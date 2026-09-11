using System;
using System.IO.Pipes;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Globalization;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;
using static System.Runtime.InteropServices.JavaScript.JSType;
using CookingBot.Core.Entities;
using CookingBot.Core.Exceptions;
using CookingBot.Core.Services;
using CookingBot.Helpers;
using CookingBot.TelegramBot.Dto;
using CookingBot.TelegramBot.Scenarios;
using static CookingBot.TelegramBot.Scenarios.ScenarioContext;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace CookingBot.TelegramBot
{
    internal class UpdateHandler : IUpdateHandler
    {
        private enum HandlerState
        {
            AwaitingStart,              // ожидает команды /start
            AwaitingRegistration,       // ожидает "Y" для регистрации
            AwaitingRegistrationName,   // ожидает name
            AwaitingChangeName,         // ожидает новое имя
            AwaitingConfigLimit,        // ожидает новое значение лимита
            Ready,
            AwaitingFindName,           // ожидает имя для /find
            AwaitingFindAllName         // ожидает имя для /findall
        }

        private enum PrefixedCommand
        {
            Unknown = -1,
            SetState,
            ChangeName,
            DeleteAccount,
            ConfirmDelete,
            Profile,
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
        private HandlerState _state = HandlerState.AwaitingStart;
        private Guid _ChangeNameTargetUserId;
        private string _configLimitTarget = string.Empty;
        private readonly string _settingsPath;
        private readonly object _stateSync = new object();
        private readonly ConcurrentDictionary<long, SemaphoreSlim> _userLocks = new();

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

        public Task HandleErrorAsync(ITelegramBotClient telegramBotClient, Exception exception,
            HandleErrorSource source, CancellationToken ct)
        {
            FileLogger.LogError(exception, $"HandleError {source}");
            return Task.CompletedTask;
        }

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

        private async Task CancelScenarioAsync(ITelegramBotClient telegramBotClient, Chat chat,
            long userId, CancellationToken ct)
        {
            var ctx = await _contextRepository.GetContext(userId, ct);
            if (ctx != null)
            {
                await _contextRepository.ResetContext(userId, ct);
            }
            await telegramBotClient.SendMessage(chat, "Сценарий отменён.",
                replyMarkup: new ReplyKeyboardRemove(), cancellationToken: ct);
            await SendMainMenuAsync(telegramBotClient, chat, userId, ct);
        }

        private static bool IsBotCommand(string text)
        {
            var commands = new[] { "/start", "/cook", "/my", "/info", "/help", "/exit", "/admin" };
            return commands.Contains(text);
        }

        private async Task HandleStateAsync(ITelegramBotClient telegramBotClient,
            string text, Chat chat, long userId, CancellationToken ct)
        {
            HandlerState state = GetState();

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
                    SetState(HandlerState.Ready);
                    break;
                case HandlerState.AwaitingChangeName:
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        await _userService.ChangeNameUser(_ChangeNameTargetUserId, text, ct);
                        SetState(HandlerState.Ready);
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
                    SetState(HandlerState.Ready);
                    await SendMainMenuAsync(telegramBotClient, chat, userId, ct);
                    break;
                case HandlerState.AwaitingFindAllName:
                    await FindRecipesAsync(text, telegramBotClient, chat, ct);
                    SetState(HandlerState.Ready);
                    await SendMainMenuAsync(telegramBotClient, chat, userId, ct);
                    break;
                case HandlerState.AwaitingConfigLimit:
                    await UpdateConfigLimitAsync(telegramBotClient, chat, text, ct);
                    break;
                case HandlerState.Ready:
                    await SendMainMenuAsync(telegramBotClient, chat, userId, ct);
                    break;
            }
        }

        private async Task HandleBotCommandAsync(ITelegramBotClient telegramBotClient,
            string text, Chat chat, long userId, CancellationToken ct)
        {
            var ctx = await _contextRepository.GetContext(userId, ct);
            if (ctx != null)
            {
                await _contextRepository.ResetContext(userId, ct);
                await telegramBotClient.SendMessage(chat, "Сценарий отменён. Данные не сохранены.", cancellationToken: ct);
            }
            SetState(HandlerState.Ready);

            switch (text)
            {
                case "/start":
                    SetState(HandlerState.AwaitingStart);
                    await StartAsync(telegramBotClient, chat, userId, ct);
                    break;
                case "/cook":
                    await SendCookMenuAsync(telegramBotClient, chat, userId, ct);
                    break;
                case "/my":
                    await SendProfileMenuAsync(telegramBotClient, chat, userId, ct);
                    break;
                case "/info":
                    await InfoAsync(telegramBotClient, chat, ct);
                    break;
                case "/help":
                    await HelpAsync(telegramBotClient, chat, userId, ct);
                    break;
                case "/exit":
                    {
                        SetState(HandlerState.AwaitingStart);
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

        private static PrefixedCommand GetPrefixedCommand(string data)
        {
            foreach (var kvp in _prefixedCommands)
            {
                if (data.StartsWith(kvp.Key))
                    return kvp.Value;
            }

            return PrefixedCommand.Unknown;
        }

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

        private async Task SendCookMenuAsync(ITelegramBotClient telegramBotClient, Chat chat,
            long userId, CancellationToken ct)
        {
            var user = await _userService.GetUserAsync(userId, ct);
            bool isRegistered = user != null;
            await telegramBotClient.SendMessage(chat, "Работа с рецептами:",
                replyMarkup: Keyboards.BuildCookMenuKeyboard(isRegistered), cancellationToken: ct);
        }

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

        private async Task SendAdminMenuAsync(ITelegramBotClient telegramBotClient, Chat chat,
            long userId, CancellationToken ct)
        {
            var user = await _userService.GetUserAsync(userId, ct);
            if (user != null &&
                (user.State == ToDoUser.ToDoUserState.Admin ||
                user.State == ToDoUser.ToDoUserState.Moderator))
            {
                await telegramBotClient.SendMessage(chat, "Администрирование:",
                    replyMarkup: Keyboards.BuildAdminMenuKeyboard(user.State), cancellationToken: ct);
            }
            else
            {
                var prompt = "У вас нет прав доступа к этому разделу.";
                await telegramBotClient.SendMessage(chat, prompt, cancellationToken: ct);
            }
        }

        private async Task FindRecipesAsync(string namePrefix, ITelegramBotClient telegramBotClient,
            Chat chat, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(namePrefix))
            {
                var listTasks = await _todoService.FindAllAsync(namePrefix, ct);

                if (listTasks.Count > 0)
                {
                    for (int i = 0; i < listTasks.Count; i++)
                    {
                        var prompt = $"{i + 1}. {listTasks[i].Name} - {listTasks[i].CreatedAt} - {listTasks[i].Id}";
                        await telegramBotClient.SendMessage(chat, prompt, cancellationToken: ct);
                    }
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

        private async Task HandleCallbackQueryAsync(ITelegramBotClient telegramBotClient,
            CallbackQuery callbackQuery, CancellationToken ct)
        {
            // защита от пустого callbackQuery.Data
            var data = callbackQuery.Data;
            if (string.IsNullOrEmpty(data))
                return;

            // отвечаем, что нажатие принято
            await telegramBotClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: ct);

            // извлекаем нажатие
            var chat = callbackQuery.Message!.Chat;
            long userId = callbackQuery.From.Id;

            // проверка активного сценария - callback передается в сценарий
            var scenarioContext = await _contextRepository.GetContext(userId, ct);
            if (scenarioContext != null)
            {
                var callbackUpdate = new Update { CallbackQuery = callbackQuery };
                await ProcessScenarioAsync(telegramBotClient, callbackUpdate, scenarioContext, userId, ct);
                return;
            }

            // запуск DeleteTaskScenario при нажатии "❌Удалить"
            if (data.StartsWith("deletetask|"))
            {
                var dto = ToDoItemCallbackDto.FromString(data);
                var ctx = new ScenarioContext(ScenarioType.DeleteTask);
                ctx.UserId = userId;
                ctx.Data["taskId"] = dto.ToDoItemId;
                var startUpdate = new Update { Message = callbackQuery.Message };
                await ProcessScenarioAsync(telegramBotClient, startUpdate, ctx, userId, ct);
                return;
            }

            // task-команды (showtask/completetask)
            if (data.StartsWith("showtask|") || data.StartsWith("completetask|"))
            {
                await HandleTaskCallbackAsync(telegramBotClient, data, chat, ct);
                return;
            }

            await HandleCallbackSwitchAsync(telegramBotClient, data, chat, userId, callbackQuery, ct);
        }

        private async Task HandleCallbackSwitchAsync(ITelegramBotClient telegramBotClient,
            string data, Chat chat, long userId, CallbackQuery callbackQuery, CancellationToken ct)
        {
            switch (data)
            {
                case "addlist":
                    {
                        var prompt = "Создание списка. Для отмены нажмите \"Отмена\".";
                        await StartScenarioAsync(telegramBotClient, ScenarioType.AddList, prompt, chat,
                            userId, callbackQuery, ct);
                        break;
                    }
                case "deletelist":
                    {
                        var prompt = "Удаление списка. Для отмены нажмите \"Отмена\".";
                        await StartScenarioAsync(telegramBotClient, ScenarioType.DeleteList, prompt, chat,
                            userId, callbackQuery, ct);
                        break;
                    }
                case "reg_yes":
                case "reg_no":
                case "reg_default":
                    await HandleRegistrationCallbackAsync(telegramBotClient, callbackQuery, data, chat,
                        userId, ct);
                    break;
                case "mainmenu":
                    SetState(HandlerState.Ready);
                    await SendMainMenuAsync(telegramBotClient, chat, userId, ct);
                    break;
                case "cook_menu":
                    await SendCookMenuAsync(telegramBotClient, chat, userId, ct);
                    break;
                case "profile_menu":
                    await SendProfileMenuAsync(telegramBotClient, chat, userId, ct);
                    break;
                case "admin_menu":
                    await SendAdminMenuAsync(telegramBotClient, chat, userId, ct);
                    break;
                case "add_recipe":
                    {
                        var prompt = "Добавление рецепта. Для отмены нажмите \"Отмена\".";
                        await StartScenarioAsync(telegramBotClient, ScenarioType.AddTask, prompt, chat,
                            userId, callbackQuery, ct);
                        break;
                    }
                case "find_recipe":
                case "findall_recipe":
                    SetState(HandlerState.AwaitingFindAllName);
                    await telegramBotClient.SendMessage(chat, "Введите имя для поиска:",
                        replyMarkup: Keyboards.BuildCancelKeyboard(), cancellationToken: ct);
                    break;
                case "findmy_recipe":
                    SetState(HandlerState.AwaitingFindName);
                    await telegramBotClient.SendMessage(chat, "Введите имя для поиска:",
                        replyMarkup: Keyboards.BuildCancelKeyboard(), cancellationToken: ct);
                    break;
                case "show_recipes":
                case "showall_recipes":
                    await ShowAllRecipesAsync(telegramBotClient, chat, ct);
                    break;
                case "showmy_recipes":
                    await ShowListsAsync(telegramBotClient, chat, userId, ct);
                    break;
                case "mod_listusers":
                case "mod_promote_member":
                case "mod_demote_guest":
                case "admin_promote_mod":
                case "admin_promote_admin":
                case "admin_demote_advanced":
                case "admin_demote_mod":
                case "admin_limits":
                case "admin_cleanup_locks":
                case "config_MaxTasks":
                case "config_MaxLengthTask":
                case "config_MaxListsPerUser":
                case "config_MaxRecipesPerList":
                    await HandleAdminCallbackAsync(telegramBotClient, data, chat, userId, ct);
                    break;
                default:
                    await HandlePrefixedCallbackAsync(telegramBotClient, data, chat, userId,
                        callbackQuery.Message!.MessageId, ct);
                    break;
            }
        }

        private async Task StartScenarioAsync(ITelegramBotClient telegramBotClient,
            ScenarioType type, string prompt, Chat chat, long userId, CallbackQuery callbackQuery,
            CancellationToken ct)
        {
            var ctx = new ScenarioContext(type);
            ctx.UserId = userId;
            await telegramBotClient.SendMessage(chat, prompt, replyMarkup: Keyboards.BuildCancelKeyboard(),
                cancellationToken: ct);
            var startUpdate = new Update { Message = callbackQuery.Message };
            await ProcessScenarioAsync(telegramBotClient, startUpdate, ctx, userId, ct);
        }

        private async Task HandleRegistrationCallbackAsync(ITelegramBotClient telegramBotClient,
            CallbackQuery callbackQuery, string data, Chat chat, long userId, CancellationToken ct)
        {
            switch (data)
            {
                case "reg_yes":
                    {
                        var displayName = callbackQuery.From?.Username ?? $"User_{userId}";
                        var prompt = $" Ваше отображаемое Имя \"{displayName}\" ";
                        await telegramBotClient.SendMessage(chat, prompt, cancellationToken: ct);
                        prompt = " Если хотите изменить, введите новое Имя. Если нет, нажмите \"Оставить по умолчанию\"";
                        await telegramBotClient.SendMessage(chat, prompt,
                            replyMarkup: Keyboards.BuildRegistrationNameKeyboard(), cancellationToken: ct);
                        SetState(HandlerState.AwaitingRegistrationName);
                        break;
                    }
                case "reg_no":
                    {
                        var prompt = " Регистрация отменена. Для начала работы выберите \"Старт\"";
                        await telegramBotClient.SendMessage(chat, prompt, cancellationToken: ct);
                        SetState(HandlerState.AwaitingStart);
                        break;
                    }
                case "reg_default":
                    {
                        await UserRegistrationAsync(telegramBotClient, chat, callbackQuery.From?.Username,
                            string.Empty, userId, ct);
                        SetState(HandlerState.Ready);
                        break;
                    }
            }
        }

        private async Task HandleAdminCallbackAsync(ITelegramBotClient telegramBotClient,
            string data, Chat chat, long userId, CancellationToken ct)
        {
            var user = await _userService.GetUserAsync(userId, ct);
            if (user == null || (user.State != ToDoUser.ToDoUserState.Moderator &&
                user.State != ToDoUser.ToDoUserState.Admin))
            {
                var prompt = "У вас нет прав доступа к этому разделу.";
                await telegramBotClient.SendMessage(chat, prompt, cancellationToken: ct);
                return;
            }

            var adminOnly = data is "admin_promote_mod" or "admin_promote_admin" or
                "admin_demote_advanced" or "admin_demote_mod" or "admin_limits" or
                "config_MaxTasks" or "config_MaxLengthTask" or "config_MaxListsPerUser" or
                "config_MaxRecipesPerList";

            if (adminOnly && user.State != ToDoUser.ToDoUserState.Admin)
            {
                var prompt = "Требуются права Администратора.";
                await telegramBotClient.SendMessage(chat, prompt, cancellationToken: ct);
                return;
            }

            switch (data)
            {
                case "mod_listusers":
                    await ListUsersAsync(telegramBotClient, chat, ct);
                    await SendMainMenuAsync(telegramBotClient, chat, userId, ct);
                    break;
                case "mod_promote_member":
                    {
                        var prompt = "Выберите пользователя для повышения до Member:";
                        await ShowUserListForStateChangeAsync(telegramBotClient, chat, "setstate_member", prompt, ct);
                        break;
                    }
                case "mod_demote_guest":
                    {
                        var prompt = "Выберите пользователя для понижения до Guest:";
                        await ShowUserListForStateChangeAsync(telegramBotClient, chat, "setstate_guest", prompt, ct);
                        break;
                    }
                case "admin_promote_mod":
                    {
                        var prompt = "Выберите пользователя для повышения до Moderator:";
                        await ShowUserListForStateChangeAsync(telegramBotClient, chat, "setstate_moderator", prompt, ct);
                        break;
                    }
                case "admin_promote_admin":
                    {
                        var prompt = "Выберите пользователя для повышения до Admin:";
                        await ShowUserListForStateChangeAsync(telegramBotClient, chat, "setstate_admin", prompt, ct);
                        break;
                    }
                case "admin_demote_advanced":
                    {
                        var prompt = "Выберите пользователя для понижения до Advanced:";
                        await ShowUserListForStateChangeAsync(telegramBotClient, chat, "setstate_advanced", prompt, ct);
                        break;
                    }
                case "admin_demote_mod":
                    {
                        var prompt = "Выберите пользователя для понижения до Moderator:";
                        await ShowUserListForStateChangeAsync(telegramBotClient, chat, "setstate_moderator", prompt, ct);
                        break;
                    }
                case "admin_limits":
                    await ShowLimitsAsync(telegramBotClient, chat, ct);
                    break;
                case "admin_cleanup_locks":
                    await CleanupGuestLocksAsync(telegramBotClient, chat, ct);
                    break;
                case "config_MaxTasks":
                case "config_MaxLengthTask":
                case "config_MaxListsPerUser":
                case "config_MaxRecipesPerList":
                    {
                        _configLimitTarget = data.Substring("config_".Length);
                        SetState(HandlerState.AwaitingConfigLimit);
                        var prompt = $"Введите новое значение для {_configLimitTarget} (1-1000):";
                        await telegramBotClient.SendMessage(chat, prompt,
                            replyMarkup: Keyboards.BuildCancelKeyboard("admin_limits"), cancellationToken: ct);
                        break;
                    }
            }
        }

        private async Task ShowLimitsAsync(ITelegramBotClient telegramBotClient, Chat chat,
            CancellationToken ct)
        {
            var (maxTasks, maxLengthTask, maxListsPerUser, maxRecipesPerList) = ReadConfigLimits();
            var str = new StringBuilder();
            str.AppendLine(" Текущие лимиты:");
            str.AppendLine($" MaxTasks: {maxTasks}");
            str.AppendLine($" MaxLengthTask: {maxLengthTask}");
            str.AppendLine($" MaxListsPerUser: {maxListsPerUser}");
            str.AppendLine($" MaxRecipesPerList: {maxRecipesPerList}");
            await telegramBotClient.SendMessage(chat, str.ToString(),
                replyMarkup: Keyboards.BuildLimitsKeyboard(), cancellationToken: ct);
        }

        private (int maxTasks, int maxLengthTask, int maxListsPerUser, int maxRecipesPerList) ReadConfigLimits()
        {
            var json = File.ReadAllText(_settingsPath);
            using var doc = JsonDocument.Parse(json);
            int maxTasks = doc.RootElement.TryGetProperty("MaxTasks", out var mt) ? mt.GetInt32() : 100;
            int maxLengthTask = doc.RootElement.TryGetProperty("MaxLengthTask", out var mlt) ? mlt.GetInt32() : 100;
            int maxListsPerUser = doc.RootElement.TryGetProperty("MaxListsPerUser", out var ml) ? ml.GetInt32() : 10;
            int maxRecipesPerList = doc.RootElement.TryGetProperty("MaxRecipesPerList", out var mr) ? mr.GetInt32() : 50;
            return (maxTasks, maxLengthTask, maxListsPerUser, maxRecipesPerList);
        }

        private async Task HandlePrefixedCallbackAsync(ITelegramBotClient telegramBotClient,
            string data, Chat chat, long userId, int messageId, CancellationToken ct)
        {
            // pipe-разделённые команды (через DTO)
            if (data.StartsWith("show|"))
            {
                await HandleShowListAsync(telegramBotClient, data, chat, userId, messageId, ct);
                return;
            }
            if (data.StartsWith("showall|"))
            {
                await HandleShowAllAsync(telegramBotClient, data, chat, messageId, ct);
                return;
            }
            if (data.StartsWith("show_completed|"))
            {
                await HandleShowCompletedAsync(telegramBotClient, data, chat, userId, messageId, ct);
                return;
            }

            // underscore-префиксы через enum
            var command = GetPrefixedCommand(data);
            switch (command)
            {
                case PrefixedCommand.SetState:
                    await HandleSetStateCallbackAsync(telegramBotClient, chat, data, userId, ct);
                    break;
                case PrefixedCommand.ChangeName:
                    if (TryGetTargetUserId(data, out Guid changeNameUserId))
                    {
                        SetState(HandlerState.AwaitingChangeName);
                        await telegramBotClient.SendMessage(chat, "Введите новое имя",
                            replyMarkup: Keyboards.BuildCancelKeyboard(), cancellationToken: ct);
                        _ChangeNameTargetUserId = changeNameUserId;
                    }
                    break;
                case PrefixedCommand.DeleteAccount:
                    if (TryGetTargetUserId(data, out Guid deleteUserId))
                    {
                        var prompt = "Вы уверены? Все ваши данные будут удалены.";
                        await telegramBotClient.SendMessage(chat, prompt, replyMarkup:
                            Keyboards.BuildDeleteAccountKeyboard(deleteUserId), cancellationToken: ct);
                    }
                    break;
                case PrefixedCommand.ConfirmDelete:
                    if (TryGetTargetUserId(data, out Guid confirmUserId))
                    {
                        await _userService.DeleteUserByUserIdAsync(confirmUserId, ct);
                    }
                    break;
                case PrefixedCommand.Profile:
                    await ShowProfileInfoAsync(telegramBotClient, chat, userId, ct);
                    break;
                case PrefixedCommand.ShowReport:
                    await ReportAsync(telegramBotClient, chat, userId, ct);
                    break;
                case PrefixedCommand.Unknown:
                    await telegramBotClient.SendMessage(chat, "Неизвестная команда.", cancellationToken: ct);
                    break;
            }
        }

        private async Task HandleShowCompletedAsync(ITelegramBotClient telegramBotClient,
            string data, Chat chat, long userId, int messageId, CancellationToken ct)
        {
            var dto = PagedListCallbackDto.FromString(data);
            var user = await _userService.GetUserAsync(userId, ct);
            if (user == null) return;

            var items = await _todoService.GetByUserIdAndList(user.UserId, dto.ToDoListId, ct);
            if (items.Count == 0)
            {
                await telegramBotClient.SendMessage(chat, "В этом списке нет рецептов", cancellationToken: ct);
                return;
            }

            var buttons = BuildTaskButtons(items);
            var extraButton = new KeyValuePair<string, string>("☑️Посмотреть выполненные", new PagedListCallbackDto
            {
                Action = "show_completed",
                ToDoListId = dto.ToDoListId,
                Page = 0
            }.ToString());
            var parts = data.Split('|');
            if (parts.Length == 3)
            {
                await telegramBotClient.EditMessageText(chat, messageId, "Выберите рецепт:",
                    replyMarkup: Keyboards.BuildPagedButtons(buttons, dto, extraButton), cancellationToken: ct);
            }
            else
            {
                await telegramBotClient.SendMessage(chat, "Выберите рецепт:",
                    replyMarkup: Keyboards.BuildPagedButtons(buttons, dto, extraButton), cancellationToken: ct);
            }
        }

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
            await telegramBotClient.EditMessageText(chat, messageId, "Все рецепты:",
                replyMarkup: Keyboards.BuildPagedButtons(buttons, dto), cancellationToken: ct);
        }

        private async Task HandleShowListAsync(ITelegramBotClient telegramBotClient, string data,
            Chat chat, long userId, int messageId, CancellationToken ct)
        {
            var dto = PagedListCallbackDto.FromString(data);
            var user = await _userService.GetUserAsync(userId, ct);
            if (user == null) return;

            var allItems = await _todoService.GetByUserIdAndList(user.UserId, dto.ToDoListId, ct);
            var completedItems = allItems.Where(t => t.State == ToDoItem.ToDoItemState.Completed).ToList();

            if (completedItems.Count == 0)
            {
                await telegramBotClient.SendMessage(chat, "Задач нет", cancellationToken: ct);
                return;
            }

            var buttons = BuildTaskButtons(completedItems);
            await telegramBotClient.EditMessageText(chat, messageId, "Выполненные рецепты:",
                replyMarkup: Keyboards.BuildPagedButtons(buttons, dto), cancellationToken: ct);
        }

        private async Task HandleTaskCallbackAsync(ITelegramBotClient telegramBotClient,
            string data, Chat chat, CancellationToken ct)
        {
            var dto = ToDoItemCallbackDto.FromString(data);

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

                        var str = new StringBuilder();
                        str.AppendLine($" Описание рецепта:");
                        str.AppendLine($" Id: {task.Id}");
                        str.AppendLine($" Name: {task.Name}");
                        str.AppendLine($" CreatedAt: {task.CreatedAt}");
                        str.AppendLine($" Deadline: {task.Deadline:dd.MM.yyyy}");
                        str.AppendLine($" Category: {ToDoItem.GetCategoryName(task.Category)}");
                        str.AppendLine($" SubCategory: {task.List?.Name ?? "-"}");
                        var ingredients = task.Ingredients != null && task.Ingredients.Count > 0 ?
                            string.Join(", ", task.Ingredients) : "-";
                        str.AppendLine($" Ingredients: {ingredients}");
                        var hiddeningredients = task.HiddenIngredients != null && task.HiddenIngredients.Count > 0 ?
                            string.Join(", ", task.HiddenIngredients) : "-";
                        str.AppendLine($" HiddenIngredients: {hiddeningredients}");
                        str.AppendLine($" Steps:");
                        if (task.Steps != null && task.Steps.Count > 0)
                        {
                            for (int i = 0; i < task.Steps.Count; i++)
                                str.AppendLine($"  {i + 1}. {task.Steps[i]}");
                        }
                        else
                        {
                            str.AppendLine("  -");
                        }
                        str.AppendLine($" State: {ToDoItem.GetStateName(task.State)}");
                        str.AppendLine($" StateChangedAt: {task.StateChangedAt}");

                        await telegramBotClient.SendMessage(chat, str.ToString(),
                            replyMarkup: Keyboards.BuildTaskActionKeyboard(task.Id), cancellationToken: ct);
                        break;
                    }
                case "completetask":
                    {
                        await _todoService.MarkCompletedAsync(dto.ToDoItemId, ct);
                        await telegramBotClient.SendMessage(chat, "Рецепт отмечен как выполненный.", cancellationToken: ct);
                        break;
                    }
            }
        }

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

        private void SetState(HandlerState newState)
        {
            lock (_stateSync)
            {
                _state = newState;
            }
        }

        private HandlerState GetState()
        {
            lock (_stateSync)
            {
                return _state;
            }
        }

        private async Task SendMainMenuAsync(ITelegramBotClient telegramBotClient, Chat chat,
            long userId, CancellationToken ct)
        {
            var user = await _userService.GetUserAsync(userId, ct);
            await telegramBotClient.SendMessage(chat, "Выберите раздел:",
                replyMarkup: Keyboards.BuildMainMenuKeyboard(user), cancellationToken: ct);
        }

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
            str.AppendLine(" \"/info\" - предоставляет информацию о версии программы и дате её создания");
            str.AppendLine(" \"/exit\" - завершает сессию работы зарегистрированного пользователя");
            str.AppendLine(" В процессе работы перечень доступных команд будет меняться");
            str.AppendLine(" Команды следует выбирать из меню ☰");
            str.Append(" Некоторым командам потребуются дополнительные данные,");
            str.AppendLine(" об этом будет указано в описании команды\n");
            await telegramBotClient.SendMessage(chat, str.ToString(), cancellationToken: ct);
        }

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
                str.AppendLine($" State: {myUser.State}");
                await telegramBotClient.SendMessage(chat, str.ToString(), cancellationToken: ct);
                await telegramBotClient.SendMessage(chat, "Управление профилем:",
                    replyMarkup: Keyboards.BuildProfileKeyboard(myUser.UserId), cancellationToken: ct);
            }
            else
            {
                await telegramBotClient.SendMessage(chat, " Вы не зарегистрированы", cancellationToken: ct);
            }
        }

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
            var prompt = "Меню доступно внизу чата (кнопка \u2630).";
            await telegramBotClient.SendMessage(chat, prompt,
                replyMarkup: new ReplyKeyboardRemove(), cancellationToken: ct);
        }

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
                SetState(HandlerState.AwaitingRegistration);
            }
            else
            {
                var prompt = $" {user.TelegramUserName} Добро пожаловать";
                await telegramBotClient.SendMessage(chat, prompt, cancellationToken: ct);
                SetState(HandlerState.Ready);
            }
        }

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
            str.AppendLine(" \"/info\" - информация о версии программы и дате её создания");
            str.AppendLine(" \"/exit\" - завершает сессию пользователя");
            if (user != null)
            {
                str.AppendLine(" \"/cancel\" - отменяет текущий сценарий");
                if (user.State == ToDoUser.ToDoUserState.Admin || user.State == ToDoUser.ToDoUserState.Moderator)
                {
                    str.AppendLine(" \"/admin\" - администрирование проекта");
                }
            }
            str.AppendLine("\n Команды можно вводить текстом или выбирать из меню ☰");
            str.AppendLine(" В меню \"Рецепты\" доступны: добавление, поиск, просмотр рецептов");
            str.AppendLine(" При просмотре рецепта доступны: ✅Выполнить, ❌Удалить");
            await telegramBotClient.SendMessage(chat, str.ToString(), cancellationToken: ct);
        }

        private async Task InfoAsync(ITelegramBotClient telegramBotClient, Chat chat,
            CancellationToken ct)
        {
            string createDate = " Created 21.05.2026    ";

            Assembly assembly = Assembly.GetExecutingAssembly();
            AssemblyName assemblyName = assembly.GetName();
            Version version = assemblyName.Version!;

            await telegramBotClient.SendMessage(chat, $"{createDate} The Version used {version}", cancellationToken: ct);
        }

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
            var (total, completed, active, generatedAt) = await _toDoReportService.GetUserStatsAsync(user!.UserId, ct);
            string generatedAtStr = generatedAt.ToShortDateString();

            var str = new StringBuilder("\n");
            str.AppendLine($" Статистика по задачам на {generatedAtStr}");
            str.AppendLine($" Всего: {total}");
            str.AppendLine($" Завершенных: {completed}");
            str.AppendLine($" Активных: {active}");
            await telegramBotClient.SendMessage(chat, str.ToString(), cancellationToken: ct);
        }

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
                    for (int i = 0; i < listTasks.Count; i++)
                    {
                        var prompt = $"{i + 1}. {listTasks[i].Name} - {listTasks[i].CreatedAt} - {listTasks[i].Id}";
                        await telegramBotClient.SendMessage(chat, prompt, cancellationToken: ct);
                    }
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

        private async Task ListUsersAsync(ITelegramBotClient telegramBotClient, Chat chat,
            CancellationToken ct)
        {
            var users = await _userService.GetAllUsersAsync(ct);

            if (users.Count == 0)
            {
                await telegramBotClient.SendMessage(chat, " Список пользователей пуст", cancellationToken: ct);
                return;
            }

            var str = new StringBuilder("\n Список пользователей:\n");
            for (int i = 0; i < users.Count; i++)
            {
                str.Append($" {i + 1}. {users[i].TelegramUserName}");
                str.Append($" | TelegramId: {users[i].TelegramUserId}");
                str.AppendLine($" | State: {users[i].State}");
            }
            await telegramBotClient.SendMessage(chat, str.ToString(), cancellationToken: ct);
        }

        private async Task ShowUserListForStateChangeAsync(ITelegramBotClient telegramBotClient,
            Chat chat, string callbackPrefix, string prompt, CancellationToken ct)
        {
            var users = await _userService.GetAllUsersAsync(ct);

            if (users.Count == 0)
            {
                await telegramBotClient.SendMessage(chat, " Список пользователей пуст", cancellationToken: ct);
                return;
            }

            await telegramBotClient.SendMessage(chat, prompt,
                replyMarkup: Keyboards.BuildUserListKeyboard(users, callbackPrefix), cancellationToken: ct);
        }

        private async Task HandleSetStateCallbackAsync(ITelegramBotClient telegramBotClient,
            Chat chat, string data, long adminUserId, CancellationToken ct)
        {
            var parts = data.Split('_', 3);
            if (parts.Length < 3)
                return;

            string stateName = parts[1];
            if (!Guid.TryParse(parts[2], out Guid targetUserId))
            {
                var prompt1 = " Не удалось разобрать идентификатор пользователя";
                await telegramBotClient.SendMessage(chat, prompt1, cancellationToken: ct);
                return;
            }

            ToDoUser.ToDoUserState targetState = stateName switch
            {
                "guest" => ToDoUser.ToDoUserState.Guest,
                "member" => ToDoUser.ToDoUserState.Member,
                "advanced" => ToDoUser.ToDoUserState.Advanced,
                "moderator" => ToDoUser.ToDoUserState.Moderator,
                "admin" => ToDoUser.ToDoUserState.Admin,
                _ => ToDoUser.ToDoUserState.Guest
            };

            var adminUser = await _userService.GetUserAsync(adminUserId, ct);
            if (adminUser == null || (adminUser.State != ToDoUser.ToDoUserState.Moderator && 
                adminUser.State != ToDoUser.ToDoUserState.Admin))
            {
                var prompt1 = "У вас нет прав для выполнения этой операции.";
                await telegramBotClient.SendMessage(chat, prompt1, cancellationToken: ct);
                return;
            }

            if ((targetState == ToDoUser.ToDoUserState.Advanced || 
                targetState == ToDoUser.ToDoUserState.Moderator || 
                targetState == ToDoUser.ToDoUserState.Admin) && 
                adminUser.State != ToDoUser.ToDoUserState.Admin)
            {
                var prompt1 = "Требуются права Администратора.";
                await telegramBotClient.SendMessage(chat, prompt1, cancellationToken: ct);
                return;
            }

            await _userService.ChangeStateAsync(targetUserId, targetState, ct);

            var targetUser = await _userService.GetUserByUserIdAsync(targetUserId, ct);
            var userName = targetUser?.TelegramUserName ?? "Unknown";
            var prompt = $" Пользователь \"{userName}\" теперь имеет статус: {targetState}";
            await telegramBotClient.SendMessage(chat, prompt, cancellationToken: ct);


        }

        private IScenario? GetScenario(ScenarioType type)
        {
            foreach (var scenario in _scenarios)
            {
                if (scenario.CanHandle(type))
                    return scenario;
            }

            return null;
        }

        private async Task ProcessScenarioAsync(ITelegramBotClient telegramBotClient, Update update,
            ScenarioContext context, long userId, CancellationToken ct)
        {
            var scenario = GetScenario(context.CurrentScenario);
            if (scenario == null)
            {
                await _contextRepository.ResetContext(userId, ct);
                var chat = update.Message?.Chat ?? update.CallbackQuery?.Message?.Chat;
                if (chat != null)
                {
                    await telegramBotClient.SendMessage(chat, "Сценарий не найден.",
                        replyMarkup: new ReplyKeyboardRemove(), cancellationToken: ct);
                    await SendMainMenuAsync(telegramBotClient, chat, userId, ct);
                }
                return;
            }

            ScenarioResult result;
            try
            {
                result = await scenario.HandleMessageAsync(telegramBotClient, context, update, ct);
            }
            catch (Exception ex)
            {
                FileLogger.LogError(ex, $"ProcessScenarioAsync user: {userId}, scenario: {context.CurrentScenario}");
                await _contextRepository.ResetContext(userId, ct);
                var chat = update.Message?.Chat ?? update.CallbackQuery?.Message?.Chat;
                if (chat != null)
                {
                    await telegramBotClient.SendMessage(chat, $"Ошибка при выполнении сценария: {ex.Message}",
                        replyMarkup: new ReplyKeyboardRemove(), cancellationToken: ct);
                    await SendMainMenuAsync(telegramBotClient, chat, userId, ct);
                }
                return;
            }

            if (result == ScenarioResult.Completed)
            {
                await _contextRepository.ResetContext(userId, ct);
                var chat = update.Message?.Chat ?? update.CallbackQuery?.Message?.Chat;
                if (chat != null)
                {
                    await telegramBotClient.SendMessage(chat, "✅", replyMarkup: new ReplyKeyboardRemove(),
                        cancellationToken: ct);
                    await SendMainMenuAsync(telegramBotClient, chat, userId, ct);
                }
            }
            else
            {
                await _contextRepository.SetContext(userId, context, ct);
            }
        }

        private async Task UpdateConfigLimitAsync(ITelegramBotClient telegramBotClient, Chat chat,
            string text, CancellationToken ct)
        {
            if (!int.TryParse(text, out int value) || value < 1 || value > 1000)
            {
                await telegramBotClient.SendMessage(chat, "Введите число от 1 до 1000:", cancellationToken: ct);
                return;
            }

            var json = File.ReadAllText(_settingsPath);
            var root = JsonNode.Parse(json)?.AsObject();
            if (root == null) return;

            root[_configLimitTarget] = value;
            await File.WriteAllTextAsync(_settingsPath, root.ToString(), ct);

            var (maxTasks, maxLengthTask, maxListsPerUser, maxRecipesPerList) = ReadConfigLimits();
            await _todoService.SetConfigurationAsync(maxTasks, maxLengthTask, maxRecipesPerList, ct);
            await _toDoListService.SetConfigurationAsync(maxListsPerUser, ct);

            var prompt = $"{_configLimitTarget} = {value}. Лимит обновлён.";
            await telegramBotClient.SendMessage(chat, prompt, cancellationToken: ct);
            await ShowLimitsAsync(telegramBotClient, chat, ct);
            SetState(HandlerState.Ready);
        }

        private async Task CleanupGuestLocksAsync(ITelegramBotClient telegramBotClient, Chat chat,
            CancellationToken ct)
        {
            int removed = 0;
            var keys = _userLocks.Keys.ToList();

            foreach (var key in keys)
            {
                var user = await _userService.GetUserAsync(key, ct);
                if (user == null)
                {
                    _userLocks.TryRemove(key, out _);
                    removed++;
                }
            }
            var prompr = $"Очищено записей незарегистрированных пользователей: {removed}";
            await telegramBotClient.SendMessage(chat, prompr, cancellationToken: ct);
        }

        private static List<KeyValuePair<string, string>> BuildTaskButtons(IEnumerable<ToDoItem> items)
        {
            return items.Select(t => new KeyValuePair<string, string>(t.Name,
                new ToDoItemCallbackDto { Action = "showtask", ToDoItemId = t.Id }.ToString())).ToList();
        }
    }
}
