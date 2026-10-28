using System;
using System.Text;
using System.Threading.Tasks;
using CookingBot.Helpers;
using CookingBot.Core.Entities;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace CookingBot.TelegramBot
{
    internal partial class UpdateHandler
    {
        /// <summary>
        /// Обрабатывает кнопку «Отмена» (текст «Отмена»): сбрасывает активный сценарий,
        /// убирает ReplyKeyboardMarkup и показывает главное меню.
        /// </summary>
        /// <param name="telegramBotClient">Клиент Telegram.</param>
        /// <param name="chat">Чат с пользователем.</param>
        /// <param name="userId">Идентификатор пользователя в Telegram.</param>
        /// <param name="ct">Токен отмены операции.</param>
        private async Task CancelScenarioAsync(ITelegramBotClient telegramBotClient, Chat chat,
            long userId, CancellationToken ct)
        {
            var ctx = await _contextRepository.GetContext(userId, ct);
            if (ctx != null)
            {
                await _contextRepository.ResetContext(userId, ct);
            }
            SetState(userId, HandlerState.Ready);
            await telegramBotClient.SendMessage(chat, "Сценарий отменён.",
                replyMarkup: new ReplyKeyboardRemove(), cancellationToken: ct);
            await SendMainMenuAsync(telegramBotClient, chat, userId, ct);
        }

        /// <summary>
        /// Обрабатывает команды BotCommand-меню: /start, /cook, /my,
        /// /help, /exit, /admin. Перед выполнением сбрасывает активный сценарий
        /// (с предупреждением «Данные не сохранены»).
        /// </summary>
        /// <param name="telegramBotClient">Клиент Telegram.</param>
        /// <param name="text">Текст команды.</param>
        /// <param name="chat">Чат с пользователем.</param>
        /// <param name="userId">Идентификатор пользователя в Telegram.</param>
        /// <param name="ct">Токен отмены операции.</param>
        private async Task HandleBotCommandAsync(ITelegramBotClient telegramBotClient,
            string text, Chat chat, long userId, CancellationToken ct)
        {
            var ctx = await _contextRepository.GetContext(userId, ct);
            if (ctx != null)
            {
                await _contextRepository.ResetContext(userId, ct);
                await telegramBotClient.SendMessage(chat, "Сценарий отменён. Данные не сохранены.", cancellationToken: ct);
            }
            SetState(userId, HandlerState.Ready);

            switch (text)
            {
                case "/start":
                    SetState(userId, HandlerState.AwaitingStart);
                    await StartAsync(telegramBotClient, chat, userId, ct);
                    break;
                case "/cook":
                    await SendCookMenuAsync(telegramBotClient, chat, userId, ct);
                    break;
                case "/my":
                    await SendProfileMenuAsync(telegramBotClient, chat, userId, ct);
                    break;
                case "/help":
                    await HelpAsync(telegramBotClient, chat, userId, ct);
                    break;
                case "/exit":
                    {
                        SetState(userId, HandlerState.AwaitingStart);
                        var prompt = "Сессия завершена. Для начала введите /start";
                        await telegramBotClient.SendMessage(chat, prompt,
                            replyMarkup: new ReplyKeyboardRemove(), cancellationToken: ct);
                        break;
                    }
                case "/admin":
                    await SendAdminMenuAsync(telegramBotClient, chat, userId, ct);
                    break;
            }
        }

        /// <summary>
        /// Отправляет меню профиля (клавиатура BuildProfileMenuKeyboard);
        /// незарегистрированным — предложение /start.
        /// </summary>
        /// <param name="telegramBotClient">Клиент Telegram.</param>
        /// <param name="chat">Чат с пользователем.</param>
        /// <param name="userId">Идентификатор пользователя в Telegram.</param>
        /// <param name="ct">Токен отмены операции.</param>
        private async Task SendProfileMenuAsync(ITelegramBotClient telegramBotClient, Chat chat,
            long userId, CancellationToken ct)
        {
            var user = await _userService.GetUserAsync(userId, ct);
            if (user == null)
            {
                var prompt = "Вы не зарегистрированы. Введите /start";
                await telegramBotClient.SendMessage(chat, prompt, cancellationToken: ct);
                return;
            }
            await telegramBotClient.SendMessage(chat, "Профиль:",
                replyMarkup: Keyboards.BuildProfileMenuKeyboard(user.UserId), cancellationToken: ct);
        }

        /// <summary>Отправляет приветствие: список команд бота и правила работы с меню.</summary>
        /// <param name="telegramBotClient">Клиент Telegram.</param>
        /// <param name="chat">Чат с пользователем.</param>
        /// <param name="ct">Токен отмены операции.</param>
        private async Task ShowGreetingAsync(ITelegramBotClient telegramBotClient, Chat chat,
            CancellationToken ct)
        {
            var str = new StringBuilder("\n");
            str.AppendLine(" Приветствую Вас в проекте \"Кулинарный бот\"\n");
            str.AppendLine(" В процессе работы бота Вам будет доступно меню ☰ (слева от поля ввода)");
            str.AppendLine(" \"/start\" - используется для начала работы");
            str.AppendLine(" \"/cook\" - используется для работы с рецептами");
            str.Append(" \"/my\" - используется для просмотра и редактирования своего профиля");
            str.AppendLine(" (доступно только для зарегистрированных пользователей)");
            str.Append(" \"/help\" - отображает краткую информацию как пользоваться Ботом,");
            str.AppendLine(" также выводит список доступных команд во время работы");
            str.AppendLine(" \"/exit\" - завершает сессию работы зарегистрированного пользователя");
            str.AppendLine(" В процессе работы перечень доступных команд будет меняться");
            str.AppendLine(" Команды следует выбирать из меню ☰");
            str.Append(" Некоторым командам потребуются дополнительные данные,");
            str.AppendLine(" об этом будет указано в описании команды\n");
            await telegramBotClient.SendMessage(chat, str.ToString(), cancellationToken: ct);
        }

        /// <summary>
        /// Отправляет регистрационные данные пользователя (UserId, имя, дата,
        /// роль) и клавиатуру управления профилем.
        /// </summary>
        /// <param name="telegramBotClient">Клиент Telegram.</param>
        /// <param name="chat">Чат с пользователем.</param>
        /// <param name="userId">Идентификатор пользователя в Telegram.</param>
        /// <param name="ct">Токен отмены операции.</param>
        private async Task ShowProfileInfoAsync(ITelegramBotClient telegramBotClient, Chat chat,
            long userId, CancellationToken ct)
        {
            var myUser = await _userService.GetUserAsync(userId, ct);

            if (myUser != null)
            {
                var str = new StringBuilder();
                str.AppendLine(" Ваши регистрационные данные:");
                str.AppendLine($" UserId: {myUser.UserId}");
                str.AppendLine($" TelegramUserId: {myUser.TelegramUserId}");
                str.AppendLine($" TelegramUserName: {myUser.TelegramUserName}");
                str.AppendLine($" Registered Date: {myUser.RegisteredAt}");
                str.AppendLine($" State: {ToDoUser.GetStateName(myUser.State)}");
                await telegramBotClient.SendMessage(chat, str.ToString(), cancellationToken: ct);
                await telegramBotClient.SendMessage(chat, "Управление профилем:",
                    replyMarkup: Keyboards.BuildProfileKeyboard(myUser.UserId), cancellationToken: ct);
            }
            else
            {
                await telegramBotClient.SendMessage(chat, " Вы не зарегистрированы", cancellationToken: ct);
            }
        }

        /// <summary>
        /// Регистрирует пользователя: имя — из введённого текста, иначе —
        /// fromUsername, иначе — «User_Id»; выводит данные нового пользователя.
        /// </summary>
        /// <param name="telegramBotClient">Клиент Telegram.</param>
        /// <param name="chat">Чат с пользователем.</param>
        /// <param name="fromUsername">Имя из Telegram (fallback, если текст пуст).</param>
        /// <param name="text">Введённое имя.</param>
        /// <param name="userId">Идентификатор пользователя в Telegram.</param>
        /// <param name="ct">Токен отмены операции.</param>
        private async Task UserRegistrationAsync(ITelegramBotClient telegramBotClient, Chat chat,
            string? fromUsername, string text, long userId, CancellationToken ct)
        {
            string telegramUserName = string.IsNullOrWhiteSpace(text) ? (fromUsername ?? $"User_{userId}") : text;

            var newUser = await _userService.RegisterUserAsync(userId, telegramUserName, ct);

            var str = new StringBuilder();
            str.AppendLine(" Зарегистрирован новый Пользователь");
            str.AppendLine($" UserId: {newUser.UserId}");
            str.AppendLine($" TelegramUserId: {newUser.TelegramUserId}");
            str.AppendLine($" TelegramUserName: {newUser.TelegramUserName}");
            str.AppendLine($" Registered Date: {newUser.RegisteredAt}");
            str.AppendLine($" State: {newUser.State}");
            await telegramBotClient.SendMessage(chat, str.ToString(), cancellationToken: ct);

            // Персональное меню ☰ под роль нового пользователя.
            // scope обязателен: без него меню переписалось бы ВСЕМ пользователям.
            await telegramBotClient.SetMyCommands(Keyboards.GetCommandsForUser(newUser),
                scope: new BotCommandScopeChat { ChatId = userId }, cancellationToken: ct);

            var prompt = "Меню доступно внизу чата (кнопка \u2630).";
            await telegramBotClient.SendMessage(chat, prompt,
                replyMarkup: new ReplyKeyboardRemove(), cancellationToken: ct);
        }

        /// <summary>
        /// Обрабатывает /start: приветствие; незарегистрированным — предложение
        /// регистрации (клавиатура Да/Нет, состояние AwaitingRegistration);
        /// зарегистрированным — «Добро пожаловать» (Ready).
        /// </summary>
        /// <param name="telegramBotClient">Клиент Telegram.</param>
        /// <param name="chat">Чат с пользователем.</param>
        /// <param name="userId">Идентификатор пользователя в Telegram.</param>
        /// <param name="ct">Токен отмены операции.</param>
        private async Task StartAsync(ITelegramBotClient telegramBotClient, Chat chat, long userId,
            CancellationToken ct)
        {
            await ShowGreetingAsync(telegramBotClient, chat, ct);

            ToDoUser? user = await _userService.GetUserAsync(userId, ct);

            if (user == null)
            {
                var prompt = " Вы еще не зарегистрированы. Хотите принять участие в проекте \"Кулинарный Бот\"?";
                await telegramBotClient.SendMessage(chat, prompt, cancellationToken: ct);
                prompt = " Для регистрации нажмите кнопку ";
                await telegramBotClient.SendMessage(chat, prompt, replyMarkup: Keyboards.BuildRegistrationKeyboard(),
                    cancellationToken: ct);
                SetState(userId, HandlerState.AwaitingRegistration);
            }
            else
            {
                var prompt = $" {user.TelegramUserName} Добро пожаловать";
                await telegramBotClient.SendMessage(chat, prompt, cancellationToken: ct);
                SetState(userId, HandlerState.Ready);
            }
        }

        /// <summary>
        /// Обрабатывает /help: краткая справка по командам (состав зависит
        /// от регистрации и роли пользователя).
        /// </summary>
        /// <param name="telegramBotClient">Клиент Telegram.</param>
        /// <param name="chat">Чат с пользователем.</param>
        /// <param name="userId">Идентификатор пользователя в Telegram.</param>
        /// <param name="ct">Токен отмены операции.</param>
        private async Task HelpAsync(ITelegramBotClient telegramBotClient, Chat chat, long userId,
            CancellationToken ct)
        {
            var user = await _userService.GetUserAsync(userId, ct);

            var str = new StringBuilder("\n");
            str.AppendLine(" Вам доступны следующие команды:");
            if (user == null)
            {
                str.AppendLine(" \"/start\" - регистрация и начало работы");
            }
            if (user != null)
            {
                str.AppendLine(" \"/my\" - профиль пользователя");
            }
            str.AppendLine(" \"/cook\" - работа с рецептами");
            str.AppendLine(" \"/help\" - краткая справка по доступным командам");
            str.AppendLine(" \"/exit\" - завершает сессию пользователя");
            if (user != null)
            {
                if (user.State == ToDoUser.ToDoUserState.Admin || user.State == ToDoUser.ToDoUserState.Moderator)
                {
                    str.AppendLine(" \"/admin\" - администрирование проекта");
                }
            }
            str.AppendLine("\n Команды можно вводить текстом или выбирать из меню ☰");
            if (user == null)
            {
                str.AppendLine(" В меню \"Рецепты\" доступны: поиск, просмотр рецептов");
            }
            else
            {
                str.AppendLine(" В меню \"Рецепты\" доступны: добавление, поиск, просмотр рецептов");
            }
            str.AppendLine(" При просмотре рецепта доступны: ❌Удалить");
            str.AppendLine($"\n Версия программы: {VersionInfo.Version}");
            str.Append($" Проект создан: {VersionInfo.CreationDate}");
            await telegramBotClient.SendMessage(chat, str.ToString(), cancellationToken: ct);
        }

        /// <summary>
        /// Отправляет статистику пользователя: количество рецептов и разделов
        /// с лимитами по роли (у Moderator/Admin — «без ограничений»).
        /// </summary>
        /// <param name="telegramBotClient">Клиент Telegram.</param>
        /// <param name="chat">Чат с пользователем.</param>
        /// <param name="userId">Идентификатор пользователя в Telegram.</param>
        /// <param name="ct">Токен отмены операции.</param>
        private async Task ReportAsync(ITelegramBotClient telegramBotClient, Chat chat, long userId,
            CancellationToken ct)
        {
            var user = await _userService.GetUserAsync(userId, ct);
            if (user == null)
            {
                var prompt = " Вы не зарегистрированы. Введите \"/start\"";
                await telegramBotClient.SendMessage(chat, prompt, cancellationToken: ct);
                return;
            }
            var (totalRecipes, totalLists) = await _toDoReportService.GetUserStatsAsync(user!.UserId, ct);
            int? recipeLimit = _todoService.GetRecipeLimit(user.State);
            int? listLimit = _toDoListService.GetListLimit(user.State);
            string recipesLimitText = recipeLimit.HasValue ? $"лимит {recipeLimit.Value}" : "без ограничений";
            string listsLimitText = listLimit.HasValue ? $"лимит {listLimit.Value}" : "без ограничений";

            var str = new StringBuilder("\n");
            str.AppendLine(" Статистика:");
            str.AppendLine($" Всего рецептов: {totalRecipes} ({recipesLimitText})");
            str.AppendLine($" Разделов: {totalLists} ({listsLimitText})");
            await telegramBotClient.SendMessage(chat, str.ToString(), cancellationToken: ct);
        }
    }
}
