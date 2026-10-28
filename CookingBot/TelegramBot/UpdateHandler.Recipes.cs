using System;
using System.Text;
using System.Threading.Tasks;
using CookingBot.Core.Entities;
using CookingBot.TelegramBot.Dto;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace CookingBot.TelegramBot
{
    internal partial class UpdateHandler
    {
        /// <summary>
        /// Отправляет меню «Работа с рецептами» (состав кнопок зависит
        /// от регистрации пользователя).
        /// </summary>
        /// <param name="telegramBotClient">Клиент Telegram.</param>
        /// <param name="chat">Чат с пользователем.</param>
        /// <param name="userId">Идентификатор пользователя в Telegram.</param>
        /// <param name="ct">Токен отмены операции.</param>
        private async Task SendCookMenuAsync(ITelegramBotClient telegramBotClient, Chat chat,
            long userId, CancellationToken ct)
        {
            var user = await _userService.GetUserAsync(userId, ct);
            bool isRegistered = user != null;
            await telegramBotClient.SendMessage(chat, "Работа с рецептами:",
                replyMarkup: Keyboards.BuildCookMenuKeyboard(isRegistered), cancellationToken: ct);
        }

        /// <summary>
        /// Поиск по всем рецептам по префиксу названия (без учёта регистра);
        /// результат — текстовым списком (первые 50 записей).
        /// </summary>
        /// <param name="namePrefix">Префикс названия рецепта.</param>
        /// <param name="telegramBotClient">Клиент Telegram.</param>
        /// <param name="chat">Чат с пользователем.</param>
        /// <param name="ct">Токен отмены операции.</param>
        private async Task FindRecipesAsync(string namePrefix, ITelegramBotClient telegramBotClient,
            Chat chat, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(namePrefix))
            {
                var listTasks = await _todoService.FindAllAsync(namePrefix, ct);

                if (listTasks.Count > 0)
                {
                    // показывает первые 50 записей
                    var text = string.Join("\n", listTasks.Take(50).Select((t, i) => $"{i + 1}. {t.Name} - {t.CreatedAt} - {t.Id}"));
                    await telegramBotClient.SendMessage(chat, text, cancellationToken: ct);
                }
                else
                {
                    await telegramBotClient.SendMessage(chat, " Список задач пуст", cancellationToken: ct);
                }
            }
            else
            {
                await telegramBotClient.SendMessage(chat, " Аргумент для команды отсутствует", cancellationToken: ct);
            }
        }

        /// <summary>
        /// Показывает все рецепты всех пользователей (постранично,
        /// редактированием текущего сообщения).
        /// </summary>
        /// <param name="telegramBotClient">Клиент Telegram.</param>
        /// <param name="data">Callback-данные кнопки («showall|Id|страница»).</param>
        /// <param name="chat">Чат с пользователем.</param>
        /// <param name="messageId">Id редактируемого сообщения.</param>
        /// <param name="ct">Токен отмены операции.</param>
        private async Task HandleShowAllAsync(ITelegramBotClient telegramBotClient, string data,
            Chat chat, int messageId, CancellationToken ct)
        {
            var dto = PagedListCallbackDto.FromString(data);
            var listAllTasks = await _todoService.GetAllTasksAsync(ct);

            if (listAllTasks.Count == 0)
            {
                await telegramBotClient.SendMessage(chat, "Список рецептов пуст", cancellationToken: ct);
                return;
            }

            var buttons = BuildTaskButtons(listAllTasks);
            await EditMessageTextSafeAsync(telegramBotClient, chat, messageId, "Все рецепты:",
                Keyboards.BuildPagedButtons(buttons, dto), ct);
        }

        /// <summary>
        /// Показывает активные рецепты пользователя в разделе (постранично)
        /// с дополнительной кнопкой «Посмотреть выполненные».
        /// </summary>
        /// <param name="telegramBotClient">Клиент Telegram.</param>
        /// <param name="data">Callback-данные кнопки («show|Id|страница»).</param>
        /// <param name="chat">Чат с пользователем.</param>
        /// <param name="userId">Идентификатор пользователя в Telegram.</param>
        /// <param name="messageId">Id редактируемого сообщения.</param>
        /// <param name="ct">Токен отмены операции.</param>
        private async Task HandleShowListAsync(ITelegramBotClient telegramBotClient, string data,
            Chat chat, long userId, int messageId, CancellationToken ct)
        {
            var dto = PagedListCallbackDto.FromString(data);
            var user = await _userService.GetUserAsync(userId, ct);
            if (user == null)
                return;

            var allItems = await _todoService.GetByUserIdAndList(user.UserId, dto.ToDoListId, ct);

            if (allItems.Count == 0)
            {
                await telegramBotClient.SendMessage(chat, "Задач нет", cancellationToken: ct);
                return;
            }

            var buttons = BuildTaskButtons(allItems);

            await EditMessageTextSafeAsync(telegramBotClient, chat, messageId, "Выберите рецепт:",
                Keyboards.BuildPagedButtons(buttons, dto), ct);
        }

        /// <summary>
        /// Обрабатывает кнопки карточки рецепта: «showtask» — карточка рецепта
        /// (все поля, включая оба вида ингредиентов; владельцу — клавиатура
        /// действий, остальным — только чтение).
        /// </summary>
        /// <param name="telegramBotClient">Клиент Telegram.</param>
        /// <param name="data">Callback-данные кнопки («showtask|Id»).</param>
        /// <param name="chat">Чат с пользователем.</param>
        /// <param name="userId">Идентификатор пользователя в Telegram.</param>
        /// <param name="ct">Токен отмены операции.</param>
        private async Task HandleTaskCallbackAsync(ITelegramBotClient telegramBotClient,
            string data, Chat chat, long userId, CancellationToken ct)
        {
            var dto = ToDoItemCallbackDto.FromString(data);
            var currentUser = await _userService.GetUserAsync(userId, ct);

            switch (dto.Action)
            {
                case "showtask":
                    {
                        var task = await _todoService.GetTaskAsync(dto.ToDoItemId, ct);
                        if (task == null)
                        {
                            await telegramBotClient.SendMessage(chat, "Рецепт не найден.", cancellationToken: ct);
                            return;
                        }

                        bool isOwner = currentUser != null && task.User.TelegramUserId == currentUser.TelegramUserId;

                        var str = new StringBuilder();
                        str.AppendLine($" Описание рецепта:");
                        str.AppendLine($" Id: {task.Id}");
                        str.AppendLine($" Name: {task.Name}");
                        str.AppendLine($" CreatedAt: {task.CreatedAt}");
                        str.AppendLine($" Category: {ToDoItem.GetCategoryName(task.Category)}");
                        str.AppendLine($" SubCategory: {task.List?.Name ?? "-"}");
                        var ingredients = task.Ingredients != null && task.Ingredients.Count > 0 ?
                            string.Join(", ", task.Ingredients) : "-";
                        str.AppendLine($" Ingredients: {ingredients}");
                        var hiddeningredients = task.HiddenIngredients != null && task.HiddenIngredients.Count > 0 ?
                            string.Join(", ", task.HiddenIngredients) : "-";
                        str.AppendLine($" HiddenIngredients: {hiddeningredients}");
                        str.AppendLine($" Steps:");
                        str.AppendLine(task.Steps != null && task.Steps.Count > 0 ? string.Join("\n", task.Steps.Select((s, i) => $" {i + 1}. {s}")) : " -");

                        await telegramBotClient.SendMessage(chat, str.ToString(),
                            replyMarkup: isOwner ?
                                Keyboards.BuildTaskActionKeyboard(task.Id) :
                                Keyboards.BuildReadOnlyTaskKeyboard(),
                            cancellationToken: ct);
                        break;
                    }
            }
        }

        /// <summary>Отправляет (новым сообщением) первую страницу списка всех рецептов.</summary>
        /// <param name="telegramBotClient">Клиент Telegram.</param>
        /// <param name="chat">Чат с пользователем.</param>
        /// <param name="ct">Токен отмены операции.</param>
        private async Task ShowAllRecipesAsync(ITelegramBotClient telegramBotClient, Chat chat,
            CancellationToken ct)
        {
            var listAllTasks = await _todoService.GetAllTasksAsync(ct);

            if (listAllTasks.Count > 0)
            {
                var buttons = BuildTaskButtons(listAllTasks);
                var dto = new PagedListCallbackDto { Action = "showall", Page = 0 };
                await telegramBotClient.SendMessage(chat, "Все рецепты:",
                    replyMarkup: Keyboards.BuildPagedButtons(buttons, dto), cancellationToken: ct);
            }
            else
            {
                await telegramBotClient.SendMessage(chat, " Список рецептов пуст", cancellationToken: ct);
            }
        }

        /// <summary>
        /// Отправляет меню разделов пользователя («Выберите список рецептов»,
        /// клавиатура BuildShowListsKeyboard); незарегистрированным — /start.
        /// </summary>
        /// <param name="telegramBotClient">Клиент Telegram.</param>
        /// <param name="chat">Чат с пользователем.</param>
        /// <param name="userId">Идентификатор пользователя в Telegram.</param>
        /// <param name="ct">Токен отмены операции.</param>
        private async Task ShowListsAsync(ITelegramBotClient telegramBotClient, Chat chat,
            long userId, CancellationToken ct)
        {
            var user = await _userService.GetUserAsync(userId, ct);
            if (user == null)
            {
                var prompt = "Вы не зарегистрированы. Введите \"/start\"";
                await telegramBotClient.SendMessage(chat, prompt, cancellationToken: ct);
                return;
            }

            var lists = await _toDoListService.GetUserListsAsync(user.UserId, ct);
            await telegramBotClient.SendMessage(chat, "Выберите список рецептов:",
                replyMarkup: Keyboards.BuildShowListsKeyboard(lists), cancellationToken: ct);
        }

        /// <summary>
        /// Поиск по рецептам пользователя по префиксу названия (без учёта
        /// регистра); результат — текстовым списком.
        /// </summary>
        /// <param name="namePrefix">Префикс названия рецепта.</param>
        /// <param name="telegramBotClient">Клиент Telegram.</param>
        /// <param name="chat">Чат с пользователем.</param>
        /// <param name="userId">Идентификатор пользователя в Telegram.</param>
        /// <param name="ct">Токен отмены операции.</param>
        private async Task FindMyRecipesAsync(string namePrefix, ITelegramBotClient telegramBotClient,
            Chat chat, long userId, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(namePrefix))
            {
                var user = await _userService.GetUserAsync(userId, ct);
                if (user == null)
                {
                    var prompt = " Вы не зарегистрированы. Введите \"/start\"";
                    await telegramBotClient.SendMessage(chat, prompt, cancellationToken: ct);
                    return;
                }
                var listTasks = await _todoService.FindAsync(user!, namePrefix, ct);

                if (listTasks.Count > 0)
                {
                    var text = string.Join("\n", listTasks.Select((t, i) => $"{i + 1}. {t.Name} - {t.CreatedAt} - {t.Id}"));
                    await telegramBotClient.SendMessage(chat, text, cancellationToken: ct);
                }
                else
                {
                    await telegramBotClient.SendMessage(chat, " Список задач пуст", cancellationToken: ct);
                }
            }
            else
            {
                var prompt = " Аргумент для команды \"/find\" отсутствует";
                await telegramBotClient.SendMessage(chat, prompt, cancellationToken: ct);
            }
        }

        /// <summary>
        /// Строит кнопки-рецепты для постраничного списка
        /// (текст — название, callback — «showtask|Id»).
        /// </summary>
        /// <param name="items">Рецепты для показа.</param>
        /// <returns>Список «текст → callback-данные» для BuildPagedButtons.</returns>
        private static List<KeyValuePair<string, string>> BuildTaskButtons(IEnumerable<ToDoItem> items)
        {
            return items.Select(t => new KeyValuePair<string, string>(t.Name,
                new ToDoItemCallbackDto { Action = "showtask", ToDoItemId = t.Id }.ToString())).ToList();
        }
    }
}
