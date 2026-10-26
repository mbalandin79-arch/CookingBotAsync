using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using CookingBot.Core.Entities;
using CookingBot.Core.Exceptions;
using CookingBot.Core.Services;
using CookingBot.Helpers;
using CookingBot.TelegramBot.Dto;
using static CookingBot.TelegramBot.Scenarios.ScenarioContext;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace CookingBot.TelegramBot.Scenarios
{
    /// <summary>
    /// Сценарий добавления рецепта (многошаговый диалог):
    /// название → дедлайн (можно пропустить) → категория (inline-кнопки) → подкатегория (или создание новой:
    /// шаг "NewListName") → ингредиенты (по ним возможен поиск) → скрытые
    /// ингредиенты (по ним поиска нет, можно пропустить) → шаги приготовления
    /// (до 20, кнопка "Готово") → создание рецепта через ToDoService
    /// (CreateRecipeAsync, ошибки лимитов/дубликата — понятным текстом).
    /// </summary>
    public class AddTaskScenario : IScenario
    {
        private readonly IUserService _userService;
        private readonly IToDoService _todoService;
        private readonly IToDoListService _todoListService;

        /// <summary>Создаёт сценарий добавления рецепта.</summary>
        /// <param name="userService">Сервис пользователей (проверка регистрации).</param>
        /// <param name="toDoService">Сервис рецептов (создание рецепта).</param>
        /// <param name="toDoListService">Сервис разделов (список подкатегорий, создание новой).</param>
        public AddTaskScenario(IUserService userService, IToDoService toDoService, IToDoListService toDoListService)
        {
            _userService = userService;
            _todoService = toDoService;
            _todoListService = toDoListService;
        }

        /// <inheritdoc/>
        public bool CanHandle(ScenarioType scenario)
        {
            if (scenario == ScenarioType.AddTask)
                return true;

            return false;
        }

        /// <inheritdoc/>
        public async Task<ScenarioResult> HandleMessageAsync(ITelegramBotClient telegramBotClient, ScenarioContext context, Update update, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var chat = update.Message?.Chat ?? update.CallbackQuery?.Message?.Chat;
            if (chat == null)
                return ScenarioResult.Completed;

            switch (context.CurrentStep)
            {
                // Шаг 1: Имя рецепта
                case null:
                    {
                        var user = await _userService.GetUserAsync(context.UserId, ct);
                        if (user == null)
                        {
                            await telegramBotClient.SendMessage(chat, "Вы не зарегистрированы. Выберите \"Старт\"", cancellationToken: ct);
                            return ScenarioResult.Completed;
                        }
                        context.Data["user"] = user;
                        await telegramBotClient.SendMessage(chat, "Введите название рецепта:", cancellationToken: ct);
                        context.CurrentStep = "Name";
                        return ScenarioResult.Transition;
                    }

                // Шаг 2: Дедлайн
                case "Name":
                    {
                        var name = update.Message?.Text;
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            await telegramBotClient.SendMessage(chat, "Название не может быть пустым. Введите название рецепта:", cancellationToken: ct);
                            return ScenarioResult.Transition;
                        }
                        name = name.Trim();
                        if(name.Length > _todoService.MaxRecipeNameLength)
                        {
                            var str = new StringBuilder();
                            str.Append($"Длина названия ({name.Length}) превышает разрешенный максимум {_todoService.MaxRecipeNameLength} символов.");
                            str.AppendLine(" Введите корректное название:");
                            await telegramBotClient.SendMessage(chat, str.ToString(), cancellationToken: ct);
                            return ScenarioResult.Transition;
                        }
                        context.Data["name"] = name;

                        await telegramBotClient.SendMessage(chat, "Введите дедлайн — дату, когда планируете приготовить блюдо,\nв формате ДД.ММ.ГГГГ.\nМожно пропустить.", replyMarkup: Keyboards.BuildSkipKeyboard(), cancellationToken: ct);
                        context.CurrentStep = "Deadline";
                        return ScenarioResult.Transition;
                    }

                // Шаг 3 (разбор ответа): дедлайн или «Пропустить»
                case "Deadline":
                    {
                        DateTime? deadline = null;

                        if (update.CallbackQuery?.Data == "cat_skip")
                        {
                            // кнопка «Пропустить» — дедлайна нет
                            deadline = null;
                        }
                        else
                        {
                            var deadlineText = update.Message?.Text;
                            if (!string.IsNullOrWhiteSpace(deadlineText))
                            {
                                // принимаем дату в формате ДД.ММ.ГГГГ (допускаем однозначные числа)
                                if (!DateTime.TryParseExact(deadlineText.Trim(), new[] { "dd.MM.yyyy", "d.M.yyyy" },
                                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                                {
                                    await telegramBotClient.SendMessage(chat, "Не удалось распознать дату. Введите дедлайн в формате ДД.ММ.ГГГГ или нажмите \"Пропустить\":", cancellationToken: ct);
                                    return ScenarioResult.Transition;
                                }

                                // введённая дата трактуется как UTC (без времени суток)
                                deadline = DateTime.SpecifyKind(parsedDate.Date, DateTimeKind.Utc);
                            }
                        }

                        // если дедлайна нет — ключ не добавляем (в Dictionary<string, object>
                        // значения-ссылки не должны быть null); при чтении — TryGetValue
                        if (deadline != null)
                        {
                            context.Data["deadline"] = deadline;
                        }

                        await telegramBotClient.SendMessage(chat, "Выберите категорию:", replyMarkup: Keyboards.BuildCategoryKeyboard(), cancellationToken: ct);
                        context.CurrentStep = "Category";
                        return ScenarioResult.Transition;
                    }

                // Шаг 3: Подкатегория
                case "Category":
                    {
                        var data = update.CallbackQuery?.Data;
                        if (string.IsNullOrEmpty(data) || !data.StartsWith("cat_"))
                        {
                            await telegramBotClient.SendMessage(chat, "Выберите категорию из списка выше:", cancellationToken: ct);
                            return ScenarioResult.Transition;
                        }

                        var catName = data.Substring("cat_".Length);
                        if (!Enum.TryParse<ToDoItem.MainCategory>(catName, out var category))
                        {
                            await telegramBotClient.SendMessage(chat, "Неизвестная категория. Выберите из списка.", cancellationToken: ct);
                            return ScenarioResult.Transition;
                        }

                        context.Data["category"] = category;

                        var user = (ToDoUser)context.Data["user"];
                        var lists = await _todoListService.GetUserListsAsync(user.UserId, ct);

                        await telegramBotClient.SendMessage(chat, "Выберите подкатегорию (список рецептов) или создайте новую:", replyMarkup: Keyboards.BuildListsKeyboard(lists), cancellationToken: ct);
                        context.CurrentStep = "SubCategory";
                        return ScenarioResult.Transition;
                    }

                // Шаг 4: Ингредиенты
                case "SubCategory":
                    {
                        var data = update.CallbackQuery?.Data;
                        if (string.IsNullOrEmpty(data)) 
                        {
                            return ScenarioResult.Transition;
                        }

                        if (data == "newlist")
                        {
                            var promp = $"Введите название подкатегории (не более {ToDoListService.MaxListNameLength} символов):";
                            await telegramBotClient.SendMessage(chat, promp, cancellationToken: ct);
                            context.CurrentStep = "NewListName";
                            return ScenarioResult.Transition;
                        }

                        var dto = ToDoListCallbackDto.FromString(data);
                        ToDoList? list = null;
                        if (dto.ToDoListId != null)
                        {
                            list = await _todoListService.GetAsync(dto.ToDoListId.Value, ct);
                        }
                        context.Data["list"] = list!;

                        var str = new StringBuilder();
                        str.AppendLine("Введите ингредиенты через запятую. По ним будет доступен поиск рецепта.");
                        str.AppendLine("Пример: мука, сахар, яйца");
                        str.AppendLine("Хотя бы один — обязательно.");
                        await telegramBotClient.SendMessage(chat, str.ToString(), cancellationToken: ct);
                        context.CurrentStep = "Ingredients";
                        return ScenarioResult.Transition;
                    }

                case "NewListName":
                    {
                        var name = update.Message?.Text;
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            var prompt = "Название не может быть пустым. Введите название подкатегории:";
                            await telegramBotClient.SendMessage(chat, prompt, cancellationToken: ct);
                            return ScenarioResult.Transition;
                        }

                        name = name.Trim();
                        if(name.Length > ToDoListService.MaxListNameLength)
                        {
                            var str = new StringBuilder();
                            str.Append($"Длина названия ({name.Length}) превышает разрешенный максимум {ToDoListService.MaxListNameLength} символов.");
                            str.AppendLine(" Введите корректное название:");
                            await telegramBotClient.SendMessage(chat, str.ToString(), cancellationToken: ct);
                            return ScenarioResult.Transition;
                        }

                        var toDoUser = (ToDoUser)context.Data["user"];

                        try
                        {
                            var list = await _todoListService.AddAsync(toDoUser, name, ct);
                            context.Data["list"] = list;
                            var str = new StringBuilder();
                            str.AppendLine("Введите ингредиенты через запятую. По ним будет доступен поиск рецепта.");
                            str.AppendLine("Пример: мука, сахар, яйца");
                            str.AppendLine("Хотя бы один — обязательно.");
                            await telegramBotClient.SendMessage(chat, str.ToString(), cancellationToken: ct);
                            context.CurrentStep = "Ingredients";
                            return ScenarioResult.Transition;
                        }
                        catch (TaskLengthLimitException e)
                        {
                            var prompt = $"Длина названия '{e.TaskLength}' превышает максимум {e.TaskLengthLimit} символов";
                            await telegramBotClient.SendMessage(chat, prompt, cancellationToken: ct);
                            return ScenarioResult.Transition;
                        }
                        catch (DuplicateTaskException e)
                        {
                            var prompt = $"Подкатегория '{e.Task}' уже существует. Введите другое название:";
                            await telegramBotClient.SendMessage(chat, prompt, cancellationToken: ct);
                            return ScenarioResult.Transition;
                        }
                        catch (ListCountLimitException e)
                        {
                            var prompt = $"Превышено максимальное количество подкатегорий равное {e.ListCountLimit}";
                            await telegramBotClient.SendMessage(chat, prompt, cancellationToken: ct);
                            return ScenarioResult.Completed;
                        }
                    }

                // Шаг 5: Скрытые ингредиенты
                case "Ingredients":
                    {
                        var ingredientsText = update.Message?.Text;
                        if (string.IsNullOrWhiteSpace(ingredientsText))
                        {
                            await telegramBotClient.SendMessage(chat, "Ингредиенты не могут быть пустыми. Введите хотя бы один:", cancellationToken: ct);
                            return ScenarioResult.Transition;
                        }

                        var ingredients = ingredientsText.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                                         .Select(s => s.Trim())
                                                         .Where(s => !string.IsNullOrEmpty(s))
                                                         .ToList();

                        if (ingredients.Count == 0)
                        {
                            await telegramBotClient.SendMessage(chat, "Ингредиенты не могут быть пустыми. Введите хотя бы один:", cancellationToken: ct);
                            return ScenarioResult.Transition;
                        }

                        context.Data["ingredients"] = ingredients;

                        await telegramBotClient.SendMessage(chat, "Введите скрытые ингредиенты через запятую. По ним поиск рецепта будет недоступен.\nМожно пропустить.", replyMarkup: Keyboards.BuildSkipKeyboard(), cancellationToken: ct);
                        context.CurrentStep = "HiddenIngredients";
                        return ScenarioResult.Transition;
                    }

                // Шаг 6: Шаги приготовления
                case "HiddenIngredients":
                    {
                        if (update.CallbackQuery?.Data == "cat_skip")
                        {
                            context.Data["hiddenIngredients"] = new List<string>();
                        }
                        else
                        {
                            var hiddenText = update.Message?.Text;
                            if (string.IsNullOrWhiteSpace(hiddenText))
                            {
                                context.Data["hiddenIngredients"] = new List<string>();
                            }
                            else
                            {
                                context.Data["hiddenIngredients"] = hiddenText.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                                                              .Select(s => s.Trim())
                                                                              .Where(s => !string.IsNullOrEmpty(s))
                                                                              .ToList();
                            }
                        }

                        context.Data["steps"] = new List<string>();
                        await telegramBotClient.SendMessage(chat, "Введите Шаг 1 приготовления:", cancellationToken: ct);
                        context.CurrentStep = "Steps";
                        return ScenarioResult.Transition;
                    }

                // Шаг 6 (продолжение): Ввод шагов
                case "Steps":
                    {
                        var steps = (List<string>)context.Data["steps"];

                        // Кнопка "Готово"
                        if (update.CallbackQuery?.Data == "steps_done")
                        {
                            if (steps.Count == 0)
                            {
                                await telegramBotClient.SendMessage(chat, "Введите хотя бы один шаг приготовления:", cancellationToken: ct);
                                return ScenarioResult.Transition;
                            }

                            // Создание рецепта
                            return await CreateRecipeAsync(telegramBotClient, chat, context, ct);
                        }

                        // Ввод шага
                        var stepText = update.Message?.Text;
                        if (string.IsNullOrWhiteSpace(stepText))
                        {
                            await telegramBotClient.SendMessage(chat, "Шаг не может быть пустым. Введите текст шага:", cancellationToken: ct);
                            return ScenarioResult.Transition;
                        }

                        steps.Add(stepText.Trim());

                        if (steps.Count >= 20)
                        {
                            await telegramBotClient.SendMessage(chat, "Достигнуто максимальное количество шагов (20). Если блюдо сложное, разбейте на несколько рецептов.", cancellationToken: ct);
                            return await CreateRecipeAsync(telegramBotClient, chat, context, ct);
                        }

                        await telegramBotClient.SendMessage(chat, $"Шаг {steps.Count} добавлен. Введите шаг {steps.Count + 1} или нажмите \"Готово\":", replyMarkup: Keyboards.BuildDoneKeyboard(), cancellationToken: ct);
                        return ScenarioResult.Transition;
                    }
                default:
                    return ScenarioResult.Completed;
            }
        }

        /// <summary>
        /// Завершающий шаг сценария: собирает данные рецепта из context.Data,
        /// создаёт рецепт через ToDoService.AddAsync и выводит карточку рецепта.
        /// Ошибки лимитов/дубликата выводятся понятным текстом, сценарий завершается.
        /// </summary>
        /// <param name="telegramBotClient">Клиент Telegram для отправки сообщений.</param>
        /// <param name="chat">Чат с пользователем.</param>
        /// <param name="context">Контекст сценария (накопленные данные).</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Completed — сценарий завершён (успех или ошибка).</returns>
        private async Task<ScenarioResult> CreateRecipeAsync(ITelegramBotClient telegramBotClient, Chat chat, ScenarioContext context, CancellationToken ct)
        {
            var toDoUser = (ToDoUser)context.Data["user"];
            var taskName = (string)context.Data["name"];
            var category = (ToDoItem.MainCategory)context.Data["category"];
            var ingredients = (List<string>)context.Data["ingredients"];
            var hiddenIngredients = (List<string>)context.Data["hiddenIngredients"];
            var steps = (List<string>)context.Data["steps"];
            var list = (ToDoList?)context.Data["list"];
            DateTime? deadline = null;
            if (context.Data.TryGetValue("deadline", out var deadlineValue))
            {
                deadline = (DateTime?)deadlineValue;
            }

            try
            {
                var item = await _todoService.AddAsync(toDoUser, taskName, category, ingredients, hiddenIngredients, steps, list, deadline, ct);

                var str = new StringBuilder();
                str.AppendLine("Рецепт добавлен:");
                str.AppendLine($" Id: {item.Id}");
                str.AppendLine($" Name: {item.Name}");
                str.AppendLine($" CreatedAt: {item.CreatedAt}");
                str.AppendLine($" Category: {ToDoItem.GetCategoryName(item.Category)}");
                str.AppendLine($" Ingredients: {string.Join(", ", item.Ingredients)}");
                str.AppendLine($" HiddenIngredients: {(item.HiddenIngredients.Count > 0 ? string.Join(", ", item.HiddenIngredients) : "-")}");
                str.AppendLine($" Steps: {item.Steps.Count}");
                str.AppendLine($" State: {ToDoItem.GetStateName(item.State)}");
                str.AppendLine($" Deadline: {(item.Deadline != null ? item.Deadline.Value.ToString("dd.MM.yyyy") : "-")}");
                await telegramBotClient.SendMessage(chat, str.ToString(), cancellationToken: ct);
                return ScenarioResult.Completed;
            }
            catch (TaskCountLimitException e)
            {
                await telegramBotClient.SendMessage(chat, $"Превышено максимальное количество задач равное {e.TaskCountLimit}", cancellationToken: ct);
                return ScenarioResult.Completed;
            }
            catch (TaskLengthLimitException e)
            {
                await telegramBotClient.SendMessage(chat, $"Длина задачи '{e.TaskLength}' превышает максимально допустимое значение {e.TaskLengthLimit}", cancellationToken: ct);
                return ScenarioResult.Completed;
            }
            catch (DuplicateTaskException e)
            {
                await telegramBotClient.SendMessage(chat, $"Задача '{e.Task}' уже существует", cancellationToken: ct);
                return ScenarioResult.Completed;
            }
            catch (ListCountLimitException e)
            {
                await telegramBotClient.SendMessage(chat, $"Превышено максимальное количество списков равное {e.ListCountLimit}", cancellationToken: ct);
                return ScenarioResult.Completed;
            }
            catch (ArgumentException e)
            {
                await telegramBotClient.SendMessage(chat, e.Message, cancellationToken: ct);
                return ScenarioResult.Completed;
            }
            catch (Exception e)
            {
                FileLogger.LogError(e, "AddTaskScenario");
                await telegramBotClient.SendMessage(chat, $"Непредвиденная ошибка: {e.Message}", cancellationToken: ct);
                return ScenarioResult.Completed;
            }
        }
    }
}
