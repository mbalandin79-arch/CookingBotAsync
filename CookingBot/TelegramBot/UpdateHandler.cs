using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using CookingBot.Core.Services;
using CookingBot.Helpers;
using CookingBot.TelegramBot.Scenarios;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Exceptions;

namespace CookingBot.TelegramBot
{
    /// <summary>
    /// Обработчик обновлений Telegram: точка входа всех сообщений и нажатий кнопок.
    /// Маршрутизация: callback-запросы → UpdateHandler.Callbacks.cs; команды →
    /// UpdateHandler.Commands.cs; работа с рецептами → UpdateHandler.Recipes.cs;
    /// администрирование → UpdateHandler.Admin.cs; сценарии — в классах
    /// Scenarios\*Scenario. Класс разбит на partial-файлы, это — главный:
    /// состояние пользователя (HandlerState), вход (HandleUpdateAsync) и утилиты.
    /// </summary>
    internal partial class UpdateHandler : IUpdateHandler
    {
        /// <summary>
        /// Состояние пользователя вне сценариев (простой конечный автомат):
        /// чего бот ждёт от пользователя в следующем текстовом сообщении.
        /// </summary>
        private enum HandlerState
        {
            /// <summary>Ожидает команды /start.</summary>
            AwaitingStart,
            /// <summary>Ожидает «Y» для регистрации.</summary>
            AwaitingRegistration,
            /// <summary>Ожидает имя.</summary>
            AwaitingRegistrationName,
            /// <summary>Ожидает новое имя.</summary>
            AwaitingChangeName,
            /// <summary>Ожидает новое значение лимита.</summary>
            AwaitingConfigLimit,
            /// <summary>Готов к обычной работе.</summary>
            Ready,
            /// <summary>Ожидает имя для /find.</summary>
            AwaitingFindName,
            /// <summary>Ожидает имя для /findall.</summary>
            AwaitingFindAllName
        }

        /// <summary>Команда с префиксом в callback-данных кнопки (например, «setstate_...»).</summary>
        private enum PrefixedCommand
        {
            /// <summary>Не распознана.</summary>
            Unknown = -1,
            /// <summary>Смена роли пользователя.</summary>
            SetState,
            /// <summary>Смена имени.</summary>
            ChangeName,
            /// <summary>Удаление аккаунта.</summary>
            DeleteAccount,
            /// <summary>Подтверждение удаления.</summary>
            ConfirmDelete,
            /// <summary>Профиль.</summary>
            Profile,
            /// <summary>Статистика пользователя.</summary>
            ShowReport
        }

        private static readonly Dictionary<string, PrefixedCommand> _prefixedCommands = new()
        {
            ["setstate_"] = PrefixedCommand.SetState,
            ["changename_"] = PrefixedCommand.ChangeName,
            ["deleteaccount_"] = PrefixedCommand.DeleteAccount,
            ["confirmdelete_"] = PrefixedCommand.ConfirmDelete,
            ["profil_"] = PrefixedCommand.Profile,
            ["show_report_"] = PrefixedCommand.ShowReport,
        };

        private readonly IUserService _userService;
        private readonly IToDoService _todoService;
        private readonly IToDoReportService _toDoReportService;
        private readonly IScenarioContextRepository _contextRepository;
        private readonly IReadOnlyList<IScenario> _scenarios;
        private readonly IToDoListService _toDoListService;
        private readonly ConcurrentDictionary<long, HandlerState> _states = new();
        private readonly ConcurrentDictionary<long, Guid> _changeNameTargets = new();
        private readonly ConcurrentDictionary<long, string> _configLimitTargets = new();
        private readonly string _settingsPath;
        private readonly ConcurrentDictionary<long, SemaphoreSlim> _userLocks = new();

        /// <summary>Создаёт обработчик обновлений.</summary>
        /// <param name="userService">Сервис пользователей.</param>
        /// <param name="todoService">Сервис рецептов.</param>
        /// <param name="toDoReportService">Сервис статистики.</param>
        /// <param name="contextRepository">Хранилище контекстов сценариев.</param>
        /// <param name="scenarios">Сценарии многошаговых диалогов.</param>
        /// <param name="toDoListService">Сервис разделов.</param>
        /// <param name="settingsPath">Путь к appsettings.json (чтение лимитов).</param>
        public UpdateHandler(IUserService userService, IToDoService todoService,
            IToDoReportService toDoReportService, IScenarioContextRepository contextRepository,
            IReadOnlyList<IScenario> scenarios, IToDoListService toDoListService, string settingsPath)
        {
            _userService = userService;
            _todoService = todoService;
            _toDoReportService = toDoReportService;
            _contextRepository = contextRepository;
            _scenarios = scenarios;
            _toDoListService = toDoListService;
            _settingsPath = settingsPath;
        }

        /// <summary>Обрабатывает ошибки библиотеки Telegram.Bot: пишет в лог и продолжает работу.</summary>
        /// <param name="telegramBotClient">Клиент Telegram.</param>
        /// <param name="exception">Исключение.</param>
        /// <param name="source">Источник ошибки.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Завершённая задача (ошибка проглатывается после записи в лог).</returns>
        public Task HandleErrorAsync(ITelegramBotClient telegramBotClient, Exception exception,
            HandleErrorSource source, CancellationToken ct)
        {
            FileLogger.LogError(exception, $"HandleError {source}");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Точка входа всех обновлений. Маршрут: нажатие кнопки →
        /// HandleCallbackQueryAsync; «Отмена»/команда → отмена сценария /
        /// HandleBotCommandAsync; активный сценарий → ProcessScenarioAsync;
        /// иначе → HandleStateAsync (по состоянию пользователя).
        /// Обновления одного пользователя обрабатываются строго по очереди
        /// (SemaphoreSlim на пользователя).
        /// </summary>
        /// <param name="telegramBotClient">Клиент Telegram.</param>
        /// <param name="update">Входящее обновление.</param>
        /// <param name="ct">Токен отмены операции.</param>
        public async Task HandleUpdateAsync(ITelegramBotClient telegramBotClient, Update update,
            CancellationToken ct)
        {
            if (update.Message == null && update.CallbackQuery == null)
                return;

            long? userId = update.Message?.From?.Id ?? update.CallbackQuery?.From?.Id;
            if (!userId.HasValue)
                return;

            var userLock = _userLocks.GetOrAdd(userId.Value, _ => new SemaphoreSlim(1, 1));
            await userLock.WaitAsync();

            try
            {
                // обработка нажатия на кнопку
                if (update.CallbackQuery != null)
                {
                    await HandleCallbackQueryAsync(telegramBotClient, update.CallbackQuery, ct);
                    return;
                }

                var message = update.Message!;
                if (string.IsNullOrWhiteSpace(message.Text))
                    return;

                var text = message.Text;

                if (text == "/cancel" || text == "Отмена")
                {
                    await CancelScenarioAsync(telegramBotClient, message.Chat, userId.Value, ct);
                    return;
                }

                if (IsBotCommand(text))
                {
                    await HandleBotCommandAsync(telegramBotClient, text, message.Chat, userId.Value, ct);
                    return;
                }

                // Проверка активного сценария
                var scenarioContext = await _contextRepository.GetContext(userId.Value, ct);
                if (scenarioContext != null)
                {
                    await ProcessScenarioAsync(telegramBotClient, update, scenarioContext, userId.Value, ct);
                    return;
                }

                try
                {
                    await HandleStateAsync(telegramBotClient, text, message.Chat, userId.Value, ct);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    FileLogger.LogError(ex, $"HandleUpdateAsync user: {userId}");
                    var prompt = " Произошла ошибка. Подробности записаны в лог.";
                    await telegramBotClient.SendMessage(update.Message!.Chat, prompt, cancellationToken: ct);
                }
            }
            finally
            {
                userLock.Release();
            }
        }

        /// <summary>Проверяет, является ли текст одной из команд BotCommand-меню бота.</summary>
        /// <param name="text">Текст сообщения.</param>
        /// <returns>true, если текст — команда бота.</returns>
        private static bool IsBotCommand(string text)
        {
            var commands = new[] { "/start", "/cook", "/my", "/info", "/help", "/exit", "/admin" };
            return commands.Contains(text);
        }

        /// <summary>
        /// Обрабатывает текстовое сообщение по текущему состоянию пользователя
        /// (HandlerState): подсказки для промежуточных состояний, регистрация,
        /// смена имени, поиск, ввод лимита; Ready — главное меню.
        /// </summary>
        /// <param name="telegramBotClient">Клиент Telegram.</param>
        /// <param name="text">Текст сообщения.</param>
        /// <param name="chat">Чат с пользователем.</param>
        /// <param name="userId">Идентификатор пользователя в Telegram.</param>
        /// <param name="ct">Токен отмены операции.</param>
        private async Task HandleStateAsync(ITelegramBotClient telegramBotClient,
            string text, Chat chat, long userId, CancellationToken ct)
        {
            HandlerState state = GetState(userId);

            switch (state)
            {
                case HandlerState.AwaitingStart:
                    {
                        var prompt = "Используйте кнопки для управления ботом";
                        await telegramBotClient.SendMessage(chat, prompt, cancellationToken: ct);
                        break;
                    }
                case HandlerState.AwaitingRegistration:
                    {
                        var prompt = "Используйте кнопки \"Да\" или \"Нет\" для подтверждения регистрации";
                        await telegramBotClient.SendMessage(chat, prompt,
                            replyMarkup: Keyboards.BuildRegistrationKeyboard(), cancellationToken: ct);
                        break;
                    }
                case HandlerState.AwaitingRegistrationName:
                    await UserRegistrationAsync(telegramBotClient, chat, null, text, userId, ct);
                    SetState(userId, HandlerState.Ready);
                    break;
                case HandlerState.AwaitingChangeName:
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        if (_changeNameTargets.TryGetValue(userId, out Guid targetUserId))
                        {
                            await _userService.ChangeNameUser(targetUserId, text, ct);
                            _changeNameTargets.TryRemove(userId, out _);
                        }
                        SetState(userId, HandlerState.Ready);
                        await telegramBotClient.SendMessage(chat, "Имя успешно изменено", cancellationToken: ct);
                        await SendMainMenuAsync(telegramBotClient, chat, userId, ct);
                    }
                    else
                    {
                        await telegramBotClient.SendMessage(chat, "Недопустимый формат имени", cancellationToken: ct);
                    }
                    break;
                case HandlerState.AwaitingFindName:
                    await FindMyRecipesAsync(text, telegramBotClient, chat, userId, ct);
                    SetState(userId, HandlerState.Ready);
                    await SendMainMenuAsync(telegramBotClient, chat, userId, ct);
                    break;
                case HandlerState.AwaitingFindAllName:
                    await FindRecipesAsync(text, telegramBotClient, chat, ct);
                    SetState(userId, HandlerState.Ready);
                    await SendMainMenuAsync(telegramBotClient, chat, userId, ct);
                    break;
                case HandlerState.AwaitingConfigLimit:
                    await UpdateConfigLimitAsync(telegramBotClient, chat, userId, text, ct);
                    break;
                case HandlerState.Ready:
                    await SendMainMenuAsync(telegramBotClient, chat, userId, ct);
                    break;
            }
        }

        /// <summary>
        /// Определяет команду с префиксом по callback-данным кнопки
        /// (по словарю _prefixedCommands).
        /// </summary>
        /// <param name="data">Callback-данные кнопки.</param>
        /// <returns>Распознанная команда; Unknown, если префикс не найден.</returns>
        private static PrefixedCommand GetPrefixedCommand(string data)
        {
            var match = _prefixedCommands.FirstOrDefault(kvp => data.StartsWith(kvp.Key));
            return match.Key == null ? PrefixedCommand.Unknown : match.Value;
        }

        /// <summary>
        /// Извлекает Id пользователя из конца callback-данных
        /// (после последнего '_').
        /// </summary>
        /// <param name="data">Callback-данные кнопки.</param>
        /// <param name="targetUserId">Извлечённый идентификатор (Guid.Empty при неудаче).</param>
        /// <returns>true, если Id удалось извлечь.</returns>
        private static bool TryGetTargetUserId(string data, out Guid targetUserId)
        {
            var index = data.LastIndexOf('_');
            if (index >= 0 && index < data.Length - 1)
            {
                return Guid.TryParse(data.Substring(index + 1), out targetUserId);
            }

            targetUserId = Guid.Empty;
            return false;
        }

        /// <summary>Устанавливает состояние пользователя (вне сценариев).</summary>
        /// <param name="userId">Идентификатор пользователя в Telegram.</param>
        /// <param name="newState">Новое состояние.</param>
        private void SetState(long userId, HandlerState newState)
        {
            _states[userId] = newState;
        }

        /// <summary>Возвращает состояние пользователя; по умолчанию — AwaitingStart.</summary>
        /// <param name="userId">Идентификатор пользователя в Telegram.</param>
        /// <returns>Текущее состояние пользователя.</returns>
        private HandlerState GetState(long userId)
        {
            return _states.TryGetValue(userId, out var state) ? state : HandlerState.AwaitingStart;
        }

        /// <summary>Отправляет пользователю главное меню («Выберите раздел:», клавиатура по роли).</summary>
        /// <param name="telegramBotClient">Клиент Telegram.</param>
        /// <param name="chat">Чат с пользователем.</param>
        /// <param name="userId">Идентификатор пользователя в Telegram.</param>
        /// <param name="ct">Токен отмены операции.</param>
        private async Task SendMainMenuAsync(ITelegramBotClient telegramBotClient, Chat chat,
            long userId, CancellationToken ct)
        {
            var user = await _userService.GetUserAsync(userId, ct);
            await telegramBotClient.SendMessage(chat, "Выберите раздел:",
                replyMarkup: Keyboards.BuildMainMenuKeyboard(user), cancellationToken: ct);
        }

        /// <summary>
        /// Редактирует текст сообщения, молча пропуская ошибку
        /// «message is not modified» (двойной клик по кнопке — не ошибка).
        /// </summary>
        /// <param name="telegramBotClient">Клиент Telegram.</param>
        /// <param name="chat">Чат с пользователем.</param>
        /// <param name="messageId">Идентификатор редактируемого сообщения.</param>
        /// <param name="text">Новый текст сообщения.</param>
        /// <param name="replyMarkup">Новая клавиатура (null — без клавиатуры).</param>
        /// <param name="ct">Токен отмены операции.</param>
        private static async Task EditMessageTextSafeAsync(ITelegramBotClient telegramBotClient, Chat chat, int messageId, 
            string text, InlineKeyboardMarkup? replyMarkup, CancellationToken ct)
        {
            try
            {
                await telegramBotClient.EditMessageText(chat, messageId, text, replyMarkup: replyMarkup, cancellationToken: ct);
            }
            catch (ApiRequestException ex) when (ex.Message.Contains("message is not modified"))
            {
                // сообщение не изменилось (например, двойной клик по кнопке) - это не ошибка
            }
        }
    }
}
