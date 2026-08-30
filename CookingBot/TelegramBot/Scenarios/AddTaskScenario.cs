using CookingBot.Core.Entities;
using CookingBot.Core.Exceptions;
using CookingBot.Core.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Telegram.Bot;
using Telegram.Bot.Types;
using static CookingBot.TelegramBot.Scenarios.ScenarioContext;

namespace CookingBot.TelegramBot.Scenarios
{
    public class AddTaskScenario : IScenario
    {
        private readonly IUserService _userService;
        private readonly IToDoService _todoService;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public AddTaskScenario(IUserService userService, IToDoService toDoService)
        {
            _userService = userService;
            _todoService = toDoService;
        }

        public bool CanHandle(ScenarioType scenario)
        {
            if (scenario == ScenarioType.AddTask)
                return true;

            return false;
        }

        public async Task<ScenarioResult> HandleMessageAsync(ITelegramBotClient telegramBotClient, ScenarioContext context, Update update, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(ct);
            try
            {
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
                            context.Data["name"] = name;
                            await telegramBotClient.SendMessage(chat, "Введите дедлайн (формат dd.MM.yyyy):", cancellationToken: ct);
                            context.CurrentStep = "Deadline";
                            return ScenarioResult.Transition;
                        }

                    // Шаг 3: Категория (кнопки)
                    case "Deadline":
                        {
                            var deadlineText = update.Message?.Text;
                            if (!DateTime.TryParseExact(deadlineText, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var deadline))
                            {
                                await telegramBotClient.SendMessage(chat, "Неверный формат даты. Введите дедлайн в формате dd.MM.yyyy:", cancellationToken: ct);
                                return ScenarioResult.Transition;
                            }

                            context.Data["deadline"] = deadline;

                            await telegramBotClient.SendMessage(chat, "Выберите категорию:", replyMarkup: Keyboards.BuildCategoryKeyboard(), cancellationToken: ct);
                            context.CurrentStep = "Category";
                            return ScenarioResult.Transition;

                            //var toDoUser = (ToDoUser)context.Data["user"];
                            //var taskName = (string)context.Data["name"];

                            //try
                            //{
                            //    var item = await _todoService.AddAsync(toDoUser, taskName, deadline, ct);
                            //    var str = new StringBuilder();
                            //    str.AppendLine("Задача добавлена:");
                            //    str.AppendLine($" Id: {item.Id}");
                            //    str.AppendLine($" Name: {item.Name}");
                            //    str.AppendLine($" CreatedAt: {item.CreatedAt}");
                            //    str.AppendLine($" Deadline: {item.Deadline:dd.MM.yyyy}");
                            //    str.AppendLine($" State: {item.State}");
                            //    await telegramBotClient.SendMessage(chat, str.ToString(), cancellationToken: ct);
                            //    return ScenarioResult.Completed;
                            //}
                            //catch (TaskCountLimitException e)
                            //{
                            //    await telegramBotClient.SendMessage(chat, $"Превышено максимальное количество задач равное {e.TaskCountLimit}", cancellationToken: ct);
                            //    return ScenarioResult.Completed;
                            //}
                            //catch (TaskLengthLimitException e)
                            //{
                            //    await telegramBotClient.SendMessage(chat, $"Длина задачи '{e.TaskLength}' превышает максимально допустимое значение {e.TaskLengthLimit}", cancellationToken: ct);
                            //    return ScenarioResult.Completed;
                            //}
                            //catch (DuplicateTaskException e)
                            //{
                            //    await telegramBotClient.SendMessage(chat, $"Задача '{e.Task}' уже существует", cancellationToken: ct);
                            //    return ScenarioResult.Completed;
                            //}
                            //catch (ArgumentException e)
                            //{
                            //    await telegramBotClient.SendMessage(chat, e.Message, cancellationToken: ct);
                            //    return ScenarioResult.Completed;
                            //}
                            //catch (Exception e)
                            //{
                            //    await telegramBotClient.SendMessage(chat, $"Непредвиденная ошибка: {e.Message}", cancellationToken: ct);
                            //    return ScenarioResult.Completed;
                            //}
                        }

                    // Шаг 4: Подкатегория
                    case "Category":
                        {
                            var data = update.CallbackQuery?.Data;
                            if(string.IsNullOrEmpty(data) || !data.StartsWith("cat_"))
                            {
                                await telegramBotClient.SendMessage(chat, "Выберите категорию из списка выше:", cancellationToken: ct);
                                return ScenarioResult.Transition;
                            }

                            var catName = data.Substring("cat_".Length);
                            if(!Enum.TryParse<ToDoItem.MainCategory>(catName, out var category))
                            {
                                await telegramBotClient.SendMessage(chat, "Неизвестная категория. Выберите из списка.", cancellationToken: ct);
                                return ScenarioResult.Transition;
                            }

                            context.Data["category"] = category;

                            await telegramBotClient.SendMessage(chat, "Введите подкатегорию или нажмите \"Пропустить\":", replyMarkup: Keyboards.BuildSkipKeyboard(), cancellationToken: ct);
                            context.CurrentStep = "SubCategory";
                            return ScenarioResult.Transition;
                        }

                    // Шаг 5: Ингредиенты
                    case "SubCategory":
                        {
                            if (update.CallbackQuery?.Data == "cat_skip")
                            {
                                context.Data["subCategory"] = null;
                            }
                            else
                            {
                                var subCat = update.Message?.Text;
                                if(string.IsNullOrWhiteSpace(subCat))
                                {
                                    await telegramBotClient.SendMessage(chat, "Подкатегория не может быть пустой. Введите текст или нажмите \"Пропустить\":", cancellationToken: ct);
                                    return ScenarioResult.Transition;
                                }
                                context.Data["subCategory"] = subCat.Trim();
                            }

                            await telegramBotClient.SendMessage(chat, "Введите ингредиенты через запятую. По ним будет доступен поиск рецепта.\nПример: мука, сахар, яйца\nХотя бы один — обязательно.", cancellationToken: ct);
                            context.CurrentStep = "Ingredients";
                            return ScenarioResult.Transition;
                        }

                    // Шаг 6: Скрытые ингредиенты
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

                    // Шаг 7: Шаги приготовления
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

                    // Шаг 7 (продолжение): Ввод шагов
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
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task<ScenarioResult> CreateRecipeAsync(ITelegramBotClient telegramBotClient, Chat chat, ScenarioContext context, CancellationToken ct)
        {
            var toDoUser = (ToDoUser)context.Data["user"];
            var taskName = (string)context.Data["name"];
            var deadline = (DateTime)context.Data["deadline"];
            var category = (ToDoItem.MainCategory)context.Data["category"];
            var subCategory = (string?)context.Data["subCategory"];
            var ingredients = (List<string>)context.Data["ingredients"];
            var hiddenIngredients = (List<string>)context.Data["hiddenIngredients"];
            var steps = (List<string>)context.Data["steps"];

            try
            {
                var item = await _todoService.AddAsync(toDoUser, taskName, deadline, category, subCategory, ingredients, hiddenIngredients, steps, new ToDoList(toDoUser, taskName), ct);

                var str = new StringBuilder();
                str.AppendLine("Рецепт добавлен:");
                str.AppendLine($" Id: {item.Id}");
                str.AppendLine($" Name: {item.Name}");
                str.AppendLine($" CreatedAt: {item.CreatedAt}");
                str.AppendLine($" Deadline: {item.Deadline:dd.MM.yyyy}");
                str.AppendLine($" Category: {ToDoItem.GetCategoryName(item.Category)}");
                str.AppendLine($" SubCategory: {item.SubCategory ?? "-"}");
                str.AppendLine($" Ingredients: {string.Join(", ", item.Ingredients)}");
                str.AppendLine($" HiddenIngredients: {(item.HiddenIngredients.Count > 0 ? string.Join(", ", item.HiddenIngredients) : "-")}");
                str.AppendLine($" Steps: {item.Steps.Count}");
                str.AppendLine($" State: {ToDoItem.GetStateName(item.State)}");
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
            catch (ArgumentException e)
            {
                await telegramBotClient.SendMessage(chat, e.Message, cancellationToken: ct);
                return ScenarioResult.Completed;
            }
            catch (Exception e)
            {
                await telegramBotClient.SendMessage(chat, $"Непредвиденная ошибка: {e.Message}", cancellationToken: ct);
                return ScenarioResult.Completed;
            }
        }
    }
}
