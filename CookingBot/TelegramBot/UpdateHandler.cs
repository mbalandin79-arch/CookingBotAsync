using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using CookingBot.Core.Entities;
using CookingBot.Core.Exceptions;
using CookingBot.Core.Services;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using CookingBot.TelegramBot.Scenarios;
using static CookingBot.TelegramBot.Scenarios.ScenarioContext;
using static System.Collections.Specialized.BitVector32;
using System.IO.Pipes;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Globalization;
using System.Threading.Tasks;

namespace CookingBot.TelegramBot
{
    internal class UpdateHandler : IUpdateHandler
    {
        private enum HandlerState
        {
            Init,                       // приветствие
            AwaitingMaxTasks,           // ожидает maxTask
            AwaitingMaxLength,          // ожидает maxLength
            AwaitingStart,              // ожидает команды /start
            AwaitingRegistration,       // ожидает "Y" для регистрации
            AwaitingRegistrationName,   // ожидает name
            AwaitingChangeName,         // ожидает новое имя
            Ready,
            AwaitingTaskIdForInfo,      // ожидает ID для /infotask
            AwaitingTaskIdForRemove,    // ожидает ID для /rempvetask
            AwaitingTaskIdForComplete,  // ожидает ID для /completetask
            AwaitingFindName,           // ожидает имя для /find
            AwaitingFindAllName         // ожидает имя для /findall
        }

        private readonly IUserService _userService;
        private readonly IToDoService _todoService;
        private readonly IToDoReportService _toDoReportService;
        private readonly IScenarioContextRepository _contextRepository;
        private readonly IReadOnlyList<IScenario> _scenarios;
        private HandlerState _state = HandlerState.Init;
        private string _displayName = "Гость";
        private Guid _ChangeNameTargetUserId;
        private int _maxTask = 0;
        private int _maxLengthTask = 0;
        private readonly object _stateSync = new object();
        private readonly SemaphoreSlim _handlelock = new SemaphoreSlim(1, 1);

        public UpdateHandler(IUserService userService, IToDoService todoService, IToDoReportService toDoReportService, IScenarioContextRepository contextRepository, IReadOnlyList<IScenario> scenarios)
        {
            _userService = userService;
            _todoService = todoService;
            _toDoReportService = toDoReportService;
            _contextRepository = contextRepository;
            _scenarios = scenarios;
        }

        public Task HandleErrorAsync(ITelegramBotClient telegramBotClient, Exception exception, HandleErrorSource source, CancellationToken ct)
        {
            Console.WriteLine($"HandleError ({source}): {exception}");
            return Task.CompletedTask;
        }

        public async Task HandleUpdateAsync(ITelegramBotClient telegramBotClient, Update update, CancellationToken ct)
        {
            if (update.Message == null && update.CallbackQuery == null)
                return;

            long? userId = update.Message?.From?.Id ?? update.CallbackQuery?.From?.Id;
            if (!userId.HasValue)
                return;

            await _handlelock.WaitAsync();

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
                    var ctx = await _contextRepository.GetContext(userId.Value, ct);
                    if (ctx != null)
                    {
                        await _contextRepository.ResetContext(userId.Value, ct);
                    }
                    await telegramBotClient.SendMessage(message.Chat, "Сценарий отменён.", replyMarkup: new ReplyKeyboardRemove(), cancellationToken: ct);
                    await SendMainMenuAsync(telegramBotClient, message.Chat, userId.Value, ct);
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
                    HandlerState state = GetState();

                    switch (state)
                    {
                        case HandlerState.Init:
                            await telegramBotClient.SendMessage(update.Message!.Chat, $"Получил '{update.Message.Text}'", cancellationToken: ct);
                            await GreetingAsync(telegramBotClient, update, ct);
                            await telegramBotClient.SendMessage(update.Message!.Chat, " Для начала введите максимально допустимое количество задач в диапазоне от 1 до 100: ", cancellationToken: ct);
                            SetState(HandlerState.AwaitingMaxTasks);
                            break;
                        case HandlerState.AwaitingMaxTasks:
                            try
                            {
                                if (string.IsNullOrWhiteSpace(text))
                                    throw new ArgumentException("Значение не соответствует требованиям");
                                _maxTask = ParseAndValidateInt(text, 1, 100);
                                await telegramBotClient.SendMessage(update.Message!.Chat, " А теперь введите максимально допустимую длину задачи в диапазоне от 1 до 100: ", cancellationToken: ct);
                                SetState(HandlerState.AwaitingMaxLength);
                            }
                            catch (ArgumentException e)
                            {
                                await telegramBotClient.SendMessage(update.Message!.Chat, $"{e.Message}. Попробуйте еще раз (1-100)", cancellationToken: ct);
                            }
                            break;
                        case HandlerState.AwaitingMaxLength:
                            try
                            {
                                if (string.IsNullOrWhiteSpace(text))
                                    throw new ArgumentException("Значение не соответствует требованиям");
                                _maxLengthTask = ParseAndValidateInt(text, 1, 100);
                                await _todoService.SetConfigurationAsync(_maxTask, _maxLengthTask, ct);
                                await telegramBotClient.SendMessage(update.Message!.Chat, " Конфигурация принята. Выберите \"Старт\" для начала работы.", cancellationToken: ct);

                                // рисуем кнопки
                                InlineKeyboardMarkup inlineKeyboardStart = new(new[]
                                    {
                                        new[] { InlineKeyboardButton.WithCallbackData("Старт", "/start") },
                                        new[] { InlineKeyboardButton.WithCallbackData("Помощь", "/help") },
                                        new[] { InlineKeyboardButton.WithCallbackData("Информация", "/info") }
                                    });

                                // показываем нарисованные кнопки
                                await telegramBotClient.SendMessage(update.Message!.Chat, "Для начала работы выберите \"Старт\"", replyMarkup: inlineKeyboardStart, cancellationToken: ct);

                                SetState(HandlerState.AwaitingStart);
                            }
                            catch (ArgumentException e)
                            {
                                await telegramBotClient.SendMessage(update.Message!.Chat, $"{e.Message}. Попробуйте еще раз (1-100)", cancellationToken: ct);
                            }
                            break;
                        case HandlerState.AwaitingStart:
                            await telegramBotClient.SendMessage(update.Message!.Chat, "Используйте кнопки для управления ботом", cancellationToken: ct);
                            break;
                        case HandlerState.AwaitingRegistration:
                            var regKeyboard = new InlineKeyboardMarkup(new[]
                            {
                                new[] { InlineKeyboardButton.WithCallbackData("Да", "reg_yes") },
                                new[] { InlineKeyboardButton.WithCallbackData("Нет", "reg_no") }
                            });
                            await telegramBotClient.SendMessage(update.Message!.Chat, "Используйте кнопки \"Да\" или \"Нет\" для подтверждения регистрации", replyMarkup: regKeyboard, cancellationToken: ct);
                            break;
                        case HandlerState.AwaitingRegistrationName:
                            await UserRegistrationAsync(telegramBotClient, update.Message!.Chat, update.Message!.From?.Username, text, userId.Value, ct);
                            SetState(HandlerState.Ready);
                            var kbUser = await _userService.GetUserAsync(userId.Value, ct);
                            await telegramBotClient.SendMessage(update.Message!.Chat, $"{_displayName}, Вы зарегистрированы. Выберите команду:", replyMarkup: Keyboards.BuildKeyboardForUser(kbUser), cancellationToken: ct);
                            break;                        
                        case HandlerState.AwaitingTaskIdForInfo:
                            await telegramBotClient.SendMessage(update.Message!.Chat, "Выберите задачу из списка выше", cancellationToken: ct);
                            break;
                        case HandlerState.AwaitingTaskIdForRemove:
                            await telegramBotClient.SendMessage(update.Message!.Chat, "Выберите задачу из списка выше", cancellationToken: ct);
                            break;
                        case HandlerState.AwaitingTaskIdForComplete:
                            await telegramBotClient.SendMessage(update.Message!.Chat, "Выберите задачу из списка выше", cancellationToken: ct);
                            break;
                        case HandlerState.AwaitingChangeName:
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                await _userService.ChangeNameUser(_ChangeNameTargetUserId, text, ct);
                                SetState(HandlerState.Ready);
                                await telegramBotClient.SendMessage(update.Message!.Chat, "Имя успешно изменено", cancellationToken: ct);
                                await SendMainMenuAsync(telegramBotClient, update.Message!.Chat, userId.Value, ct);
                            }
                            else
                            {
                                await telegramBotClient.SendMessage(update.Message!.Chat, "Недопустимый формат имени", cancellationToken: ct);
                            }
                            break;
                        case HandlerState.AwaitingFindName:
                            await FindAsync(text, telegramBotClient, update.Message!.Chat, userId.Value, ct);
                            SetState(HandlerState.Ready);
                            await SendMainMenuAsync(telegramBotClient, update.Message!.Chat, userId.Value, ct);
                            break;
                        case HandlerState.AwaitingFindAllName:
                            await FindAllAsync(text, telegramBotClient, update.Message!.Chat, ct);
                            SetState(HandlerState.Ready);
                            await SendMainMenuAsync(telegramBotClient, update.Message!.Chat, userId.Value, ct);
                            break;
                        case HandlerState.Ready:
                            await SendMainMenuAsync(telegramBotClient, update.Message!.Chat, userId.Value, ct);
                            break;
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    await telegramBotClient.SendMessage(update.Message!.Chat, $" Произошла непредвиденная ошибка:\n Тип ошибки: {ex.GetType().Name}\n Сообщение: {ex.Message}", cancellationToken: ct);
                    var trace = new StackTrace(ex, true);
                    foreach (var item in trace.GetFrames())
                    {
                        await telegramBotClient.SendMessage(update.Message!.Chat, $"Файл: {item.GetFileName()}, Строка: {item.GetFileLineNumber()}, Метод: {item.GetMethod()}", cancellationToken: ct);
                    }
                    if (ex.InnerException != null)
                    {
                        await telegramBotClient.SendMessage(update.Message!.Chat, $" Внутреннее исключение:\n Тип: {ex.InnerException.GetType().Name}\n Сообщение: {ex.InnerException.Message}\n", cancellationToken: ct);
                        var newtrace = new StackTrace(ex.InnerException, true);
                        foreach (var item in newtrace.GetFrames())
                        {
                            await telegramBotClient.SendMessage(update.Message!.Chat, $"Файл: {item.GetFileName()}, Строка: {item.GetFileLineNumber()}, Метод: {item.GetMethod()}", cancellationToken: ct);
                        }
                    }
                }
            }
            finally
            {
                _handlelock.Release();
            }
        }

        private async Task FindAllAsync(string namePrefix, ITelegramBotClient telegramBotClient, Chat chat, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(namePrefix))
            {
                var listTasks = await _todoService.FindAllAsync(namePrefix, ct);

                if (listTasks.Count > 0)
                {
                    for (int i = 0; i < listTasks.Count; i++)
                    {
                        await telegramBotClient.SendMessage(chat, $"{i + 1}. {listTasks[i].Name} - {listTasks[i].CreatedAt} - {listTasks[i].Id}", cancellationToken: ct);
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

        private async Task HandleCallbackQueryAsync(ITelegramBotClient telegramBotClient, CallbackQuery callbackQuery, CancellationToken ct)
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

            if (_maxTask == 0)
            {
                await GreetingAsync(telegramBotClient, new Update { Message = callbackQuery.Message }, ct);
                await telegramBotClient.SendMessage(chat, "Для начала введите максимально допустимое количество задач в диапазоне от 1 до 100: ", cancellationToken: ct);
                SetState(HandlerState.AwaitingStart);
                return;
            }

            switch (data)
            {
                case "/start":
                    SetState(HandlerState.AwaitingStart);
                    await MyNameIsAsync(telegramBotClient, chat, userId, ct);
                    break;
                case "/help":
                    await HelpAsync(telegramBotClient, chat, userId, ct);
                    await SendMainMenuAsync(telegramBotClient, chat, userId, ct);
                    break;
                case "/info":
                    await InfoAsync(telegramBotClient, chat, ct);
                    await SendMainMenuAsync(telegramBotClient, chat, userId, ct);
                    break;
                case "/myinfo":
                    await MyInfoAsync(telegramBotClient, chat, userId, ct);
                    await SendMainMenuAsync(telegramBotClient, chat, userId, ct);
                    break;
                case "/showtasks":
                    await ShowTasksAsync(telegramBotClient, chat, userId, ct);
                    await SendMainMenuAsync(telegramBotClient, chat, userId, ct);
                    break;
                case "/showalltasks":
                    await ShowAllTasksAsync(telegramBotClient, chat, userId, ct);
                    await SendMainMenuAsync(telegramBotClient, chat, userId, ct);
                    break;
                case "/alltasks":
                    await ShowAllTasksAsyncAsButtonsAsync(telegramBotClient, chat, ct);
                    await SendMainMenuAsync(telegramBotClient, chat, userId, ct);
                    break;
                case "/report":
                    await ReportAsync(telegramBotClient, chat, userId, ct);
                    await SendMainMenuAsync(telegramBotClient, chat, userId, ct);
                    break;
                case "/exit":
                    Environment.Exit(0);
                    break;
                case "/addtask":
                    var ctx = new ScenarioContext(ScenarioType.AddTask);
                    ctx.UserId = userId;
                    await telegramBotClient.SendMessage(chat, "Режим добавления задачи. Для отмены нажмите \"Отмена\".", replyMarkup: Keyboards.BuildCancelKeyboard(), cancellationToken: ct);
                    var startUpdate = new Update { Message = callbackQuery.Message };
                    await ProcessScenarioAsync(telegramBotClient, startUpdate, ctx, userId, ct);
                    break;
                case "/infotask":
                    SetState(HandlerState.AwaitingTaskIdForInfo);
                    await ShowTasksForActionAsync(telegramBotClient, chat, userId, "taskinfo", "Выберите задачу для просмотра информации:", ct);
                    break;
                case "/removetask":
                    SetState(HandlerState.AwaitingTaskIdForRemove);
                    await ShowTasksForActionAsync(telegramBotClient, chat, userId, "taskremove", "Выберите задачу для удаления:", ct);
                    break;
                case "/completetask":
                    SetState(HandlerState.AwaitingTaskIdForComplete);
                    await ShowTasksForActionAsync(telegramBotClient, chat, userId, "taskcomplete", "Выберите задачу для завершения:", ct);
                    break;
                case "/find":
                    SetState(HandlerState.AwaitingFindName);
                    await telegramBotClient.SendMessage(chat, "Введите имя для поиска:", replyMarkup: new InlineKeyboardMarkup(new[] { InlineKeyboardButton.WithCallbackData("Отмена", "mainmenu") }), cancellationToken: ct);
                    break;
                case "/findall":
                    SetState(HandlerState.AwaitingFindAllName);
                    await telegramBotClient.SendMessage(chat, "Введите имя для поиска:", replyMarkup: new InlineKeyboardMarkup(new[] { InlineKeyboardButton.WithCallbackData("Отмена", "mainmenu") }), cancellationToken: ct);
                    break;
                // регистрация
                case "reg_yes":
                case "reg_no":
                case "reg_default":
                    await HandleRegistrationCallbackAsync(telegramBotClient, callbackQuery, data, chat, userId, ct);
                    break;
                case "mainmenu":
                    SetState(HandlerState.Ready);
                    await SendMainMenuAsync(telegramBotClient, chat, userId, ct);
                    break;
                // только для модераторов/администраторов
                case "mod_listusers":                    
                case "mod_promote_member":                    
                case "mod_demote_guest":                   
                case "admin_promote_mod":                    
                case "admin_promote_admin":                    
                case "admin_demote_advanced":                    
                case "admin_demote_mod":
                    await HandleAdminCallbackAsync(telegramBotClient, data, chat, userId, ct);
                    break;
                default:
                    await HandlePrefixedCallbackAsync(telegramBotClient, data, chat, userId, ct);
                    break;
            }
        }

        private async Task HandleRegistrationCallbackAsync(ITelegramBotClient telegramBotClient, CallbackQuery callbackQuery, string data, Chat chat, long userId, CancellationToken ct)
        {
            switch (data)
            {
                case "reg_yes":
                    {
                        var displayName = callbackQuery.From?.Username ?? $"User_{userId}";
                        await telegramBotClient.SendMessage(chat, $" Ваше отображаемое Имя \"{displayName}\" ", cancellationToken: ct);
                        var defaultNameKeyboard = new InlineKeyboardMarkup(new[]
                        {
                            new[] { InlineKeyboardButton.WithCallbackData("Оставить по умолчанию", "reg_default") },
                            new[] { InlineKeyboardButton.WithCallbackData("Отмена", "reg_no") }
                        });
                        await telegramBotClient.SendMessage(chat, " Если хотите изменить, введите новое Имя. Если нет, нажмите \"Оставить по умолчанию\"", replyMarkup: defaultNameKeyboard, cancellationToken: ct);
                        SetState(HandlerState.AwaitingRegistrationName);
                        break;
                    }
                case "reg_no":
                    await telegramBotClient.SendMessage(chat, " Регистрация отменена. Для начала работы выберите \"Старт\"", cancellationToken: ct);
                    SetState(HandlerState.AwaitingStart);
                    break;
                case "reg_default":
                    {
                        await UserRegistrationAsync(telegramBotClient, chat, callbackQuery.From?.Username, string.Empty, userId, ct);
                        SetState(HandlerState.Ready);
                        var regUser = await _userService.GetUserAsync(userId, ct);
                        await telegramBotClient.SendMessage(chat, $"{_displayName}, Вы зарегистрированы. Выберите команду:", replyMarkup: Keyboards.BuildKeyboardForUser(regUser), cancellationToken: ct);
                        break;
                    }
            }
        }

        private async Task HandleAdminCallbackAsync(ITelegramBotClient telegramBotClient, string data, Chat chat, long userId, CancellationToken ct)
        {
            switch (data)
            {
                case "mod_listusers":
                    await ListUsersAsync(telegramBotClient, chat, ct);
                    await SendMainMenuAsync(telegramBotClient, chat, userId, ct);
                    break;
                case "mod_promote_member":
                    await ShowUserListForStateChangeAsync(telegramBotClient, chat, "setstate_member", "Выберите пользователя для повышения до Member:", ct);
                    break;
                case "mod_demote_guest":
                    await ShowUserListForStateChangeAsync(telegramBotClient, chat, "setstate_guest", "Выберите пользователя для понижения до Guest:", ct);
                    break;
                case "admin_promote_mod":
                    await ShowUserListForStateChangeAsync(telegramBotClient, chat, "setstate_moderator", "Выберите пользователя для повышения до Moderator:", ct);
                    break;
                case "admin_promote_admin":
                    await ShowUserListForStateChangeAsync(telegramBotClient, chat, "setstate_admin", "Выберите пользователя для повышения до Admin:", ct);
                    break;
                case "admin_demote_advanced":
                    await ShowUserListForStateChangeAsync(telegramBotClient, chat, "setstate_advanced", "Выберите пользователя для понижения до Advanced:", ct);
                    break;
                case "admin_demote_mod":
                    await ShowUserListForStateChangeAsync(telegramBotClient, chat, "setstate_moderator", "Выберите пользователя для понижения до Moderator:", ct);
                    break;
            }
        }

        private async Task HandlePrefixedCallbackAsync(ITelegramBotClient telegramBotClient, string data, Chat chat, long userId, CancellationToken ct)
        {
            if (data.StartsWith("setstate_"))
                await HandleSetStateCallbackAsync(telegramBotClient, chat, data, userId, ct);
            else if (data.StartsWith("taskinfo_"))
                await HandleTaskActionCallbackAsync(telegramBotClient, chat, data, userId, "info", ct);
            else if (data.StartsWith("alltaskinfo_"))
                await HandleAllTaskInfoCallbackAsync(telegramBotClient, chat, data, userId, ct);
            else if (data.StartsWith("taskremove_"))
                await HandleTaskActionCallbackAsync(telegramBotClient, chat, data, userId, "remove", ct);
            else if (data.StartsWith("taskcomplete_"))
                await HandleTaskActionCallbackAsync(telegramBotClient, chat, data, userId, "complete", ct);
            else if (data.StartsWith("changename_"))
            {
                if (Guid.TryParse(data.Substring("changename_".Length), out Guid targetUserId))
                {
                    SetState(HandlerState.AwaitingChangeName);
                    await telegramBotClient.SendMessage(chat, "Введите новое имя", replyMarkup: new InlineKeyboardMarkup(new[] { InlineKeyboardButton.WithCallbackData("Отмена", "mainmenu") }), cancellationToken: ct);
                    _ChangeNameTargetUserId = targetUserId;
                }
            }
            else if (data.StartsWith("deleteaccount_"))
            {
                if (Guid.TryParse(data.Substring("deleteaccount_".Length), out Guid targetUserId))
                {
                    var confirmKeyboard = new InlineKeyboardMarkup(new[]
                    {
                                new[] { InlineKeyboardButton.WithCallbackData("Да, удалить", $"confirmdelete_{targetUserId}") },
                                new[] { InlineKeyboardButton.WithCallbackData("Отмена", "mainmenu") }
                            });
                    await telegramBotClient.SendMessage(chat, "Вы уверены? Все ваши данные будут удалены.", replyMarkup: confirmKeyboard, cancellationToken: ct);
                }
            }
            else if (data.StartsWith("confirmdelete_"))
            {
                if (Guid.TryParse(data.Substring("confirmdelete_".Length), out Guid targetUserId))
                {
                    await _userService.DeleteUserByUserIdAsync(targetUserId, ct);
                    await telegramBotClient.SendMessage(chat, "Ваш аккаунт удалён. Для регистрации выберите \"Старт\"", replyMarkup: Keyboards.BuildKeyboardForUser(null), cancellationToken: ct);
                }
            }
        }

        private async Task HandleAllTaskInfoCallbackAsync(ITelegramBotClient telegramBotClient, Chat chat, string data, long userId, CancellationToken ct)
        {
            var parts = data.Split('_', 2);
            if (parts.Length < 2)
                return;

            if (!Guid.TryParse(parts[1], out Guid taskId))
            {
                await telegramBotClient.SendMessage(chat, " Не удалось разобрать идентификатор рецепта", cancellationToken: ct);
                return;
            }

            var task = await _todoService.GetTaskAsync(taskId, ct);
            if (task == null)
            {
                await telegramBotClient.SendMessage(chat, " Задача не найдена", cancellationToken: ct);
                return;
            }
            var str = new StringBuilder();
            str.AppendLine($" Описание рецепта:");
            str.AppendLine($" Id: {task.Id}");
            str.AppendLine($" Name: {task.Name}");
            str.AppendLine($" CreatedAt: {task.CreatedAt}");
            str.AppendLine($" Deadline: {task.Deadline:dd.MM.yyyy}");
            str.AppendLine($" Category: {ToDoItem.GetCategoryName(task.Category)}");
            str.AppendLine($" SubCategory: {task.SubCategory ?? "-"}");
            str.AppendLine($" Ingredients: {(task.Ingredients != null && task.Ingredients.Count > 0 ? string.Join(", ", task.Ingredients) : "-")}");
            str.AppendLine($" HiddenIngredients: {(task.HiddenIngredients != null && task.HiddenIngredients.Count > 0 ? string.Join(", ", task.HiddenIngredients) : "-")}");
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
            await telegramBotClient.SendMessage(chat, str.ToString(), cancellationToken: ct);

            var kbUser = await _userService.GetUserAsync(userId, ct);
            await telegramBotClient.SendMessage(chat, " Текущее меню:", replyMarkup: Keyboards.BuildKeyboardForUser(kbUser), cancellationToken: ct);
        }

        private async Task ShowAllTasksAsyncAsButtonsAsync(ITelegramBotClient telegramBotClient, Chat chat, CancellationToken ct)
        {
            var listAllTasks = await _todoService.GetAllTasksAsync(ct);

            if (listAllTasks.Count > 0)
            {
                var rows = new List<List<InlineKeyboardButton>>();
                foreach (var task in listAllTasks)
                {
                    var buttonText = task.Name;
                    var callback_data = $"alltaskinfo_{task.Id}";
                    rows.Add(new() { InlineKeyboardButton.WithCallbackData(buttonText, callback_data) });
                }
                rows.Add(new() { InlineKeyboardButton.WithCallbackData("Главное меню", "mainmenu") });

                await telegramBotClient.SendMessage(chat, "Все задачи:", replyMarkup: new InlineKeyboardMarkup(rows), cancellationToken: ct);
            }
            else
            {
                await telegramBotClient.SendMessage(chat, " Список задач пуст", cancellationToken: ct);
            }
        }

        private int ParseAndValidateInt(string? str, int min, int max)
        {
            int answ = 0;

            if (!int.TryParse(str, out answ) || answ < min || answ > max)
                throw new ArgumentException($"{str} это значение не соответствует требованиям");
            return answ;
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

        private async Task SendMainMenuAsync(ITelegramBotClient telegramBotClient, Chat chat, long userId, CancellationToken ct)
        {
            var user = await _userService.GetUserAsync(userId, ct);
            await telegramBotClient.SendMessage(chat, "Главное меню:", replyMarkup: Keyboards.BuildKeyboardForUser(user), cancellationToken: ct);
        }

        private async Task GreetingAsync(ITelegramBotClient telegramBotClient, Update update, CancellationToken ct)
        {
            var str = new StringBuilder("\n");
            str.AppendLine(" Приветствую Вас в проекте \"Кулинарный бот\"\n");
            str.AppendLine(" Перед началом работы Бота вам доступны следующие команды:");
            str.AppendLine(" \"Старт\" - используется для начала работы");
            str.AppendLine(" \"Помощь\" - отображает краткую информацию как пользоваться Ботом, также выводит список доступных команд во время работы");
            str.AppendLine(" \"Информация\" - предоставляет информацию о версии программы и дате её создания");
            str.AppendLine(" В процессе работы перечень доступных команд будет меняться");
            str.AppendLine(" Команды следует выбирать нажатием соответствующей кнопки");
            str.AppendLine(" Некоторым командам потребуются дополнительные данные, об этом будет указано в описании команды\n");
            await telegramBotClient.SendMessage(update.Message!.Chat, str.ToString(), cancellationToken: ct);
        }

        private async Task MyInfoAsync(ITelegramBotClient telegramBotClient, Chat chat, long userId, CancellationToken ct)
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
                await telegramBotClient.SendMessage(chat, "Управление профилем:", replyMarkup: Keyboards.BuildProfileKeyboard(myUser.UserId), cancellationToken: ct);
            }
            else
            {
                await telegramBotClient.SendMessage(chat, " Вы не зарегистрированы", cancellationToken: ct);
            }
        }

        private async Task UserRegistrationAsync(ITelegramBotClient telegramBotClient, Chat chat, string? fromUsername, string text, long userId, CancellationToken ct)
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
            _displayName = newUser.TelegramUserName;
        }

        private async Task MyNameIsAsync(ITelegramBotClient telegramBotClient, Chat chat, long userId, CancellationToken ct)
        {
            ToDoUser? user = await _userService.GetUserAsync(userId, ct);

            if (user == null)
            {
                await telegramBotClient.SendMessage(chat, " Вы еще не зарегистрированы. Хотите принять участие в проекте \"Кулинарный Бот\"?", cancellationToken: ct);
                var regKeyboard = new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("Да", "reg_yes") },
                    new[] { InlineKeyboardButton.WithCallbackData("Нет", "reg_no") }
                });
                await telegramBotClient.SendMessage(chat, " Для регистрации нажмите кнопку ", replyMarkup: regKeyboard, cancellationToken: ct);
                SetState(HandlerState.AwaitingRegistration);
            }
            else
            {
                _displayName = user.TelegramUserName;
                await telegramBotClient.SendMessage(chat, $" {user.TelegramUserName} Добро пожаловать", cancellationToken: ct);
                SetState(HandlerState.Ready);
                await telegramBotClient.SendMessage(chat, $"{_displayName}, Выберите команду: ", replyMarkup: Keyboards.BuildKeyboardForUser(user), cancellationToken: ct);
            }
        }

        private async Task HelpAsync(ITelegramBotClient telegramBotClient, Chat chat, long userId, CancellationToken ct)
        {
            var user = await _userService.GetUserAsync(userId, ct);

            var str = new StringBuilder("\n"); ;
            str.AppendLine(" Вам доступны следующие кнопки:");
            if (user == null)
            {
                str.AppendLine(" \"Старт\" - используется для начала работы");
            }
            str.AppendLine(" \"Помощь\" - отображает краткую информацию как пользоваться Ботом, также выводит список доступных команд во время работы");
            str.AppendLine(" \"Информация\" - предоставляет информацию о версии программы и дате её создания");
            if (user != null)
            {
                str.AppendLine(" \"Мой профиль\" - отображает краткую информацию о самом пользователе и его статусе");
                str.AppendLine(" \"Добавить задачу\" - позволяет добавить Задачу, между командой и Задачей обязательно должен быть пробел");
                //str.AppendLine(" \"/edittask Идентификатор\" - позволяет заполнить по Content у Задачу по ее Идентификатору, между командой и Идентификатором обязательно должен быть пробел");
                str.AppendLine(" \"Активные задачи\" - отображает все \"Активные\" задачи");
                str.AppendLine(" \"Все задачи\" - отображает все задачи");
                str.AppendLine(" \"Инфо о задаче\" - отображает информацию о задачи по ее Идентификатору, между командой и Идентификатором обязательно должен быть пробел");
                str.AppendLine(" \"Удалить задачу\" - позволяет удалить доступную задачу по ее Идентификатору, между командой и Идентификатором обязательно должен быть пробел");
                str.AppendLine(" \"Завершить задачу\" - позволяет изменить состояние задачи с \"Активная\" на \"Завершенная\", между командой и Идентификатором обязательно должен быть пробел");
                str.AppendLine(" \"Отчёт\" - отображает статистику по задачам текущего пользователя на данный момент времени");
                str.AppendLine(" \"Поиск\" - отображает все задачи зарегистрированного пользователя с именем \"Имя\", между командой и Именем обязательно должен быть пробел");
                str.AppendLine(" \"/cancel\" - отменяет текущий сценарий\n");
                str.AppendLine(" \"Выход\" - завершает работу Бота\n");
            }
            await telegramBotClient.SendMessage(chat, str.ToString(), cancellationToken: ct);
        }

        private async Task InfoAsync(ITelegramBotClient telegramBotClient, Chat chat, CancellationToken ct)
        {
            string createDate = " Created 21.05.2026    ";

            Assembly assembly = Assembly.GetExecutingAssembly();
            AssemblyName assemblyName = assembly.GetName();
            Version version = assemblyName.Version!;

            await telegramBotClient.SendMessage(chat, $"{createDate} The Version used {version}", cancellationToken: ct);
        }
        
        private async Task ShowTasksAsync(ITelegramBotClient telegramBotClient, Chat chat, long userId, CancellationToken ct)
        {
            var user = await _userService.GetUserAsync(userId, ct);
            if (user == null)
            {
                await telegramBotClient.SendMessage(chat, " Вы не зарегистрированы. Введите \"/start\"", cancellationToken: ct);
                return;
            }
            var listTasks = await _todoService.GetActiveByUserIdAsync(user!.UserId, ct);

            if (listTasks.Count > 0)
            {
                for (int i = 0; i < listTasks.Count; i++)
                {
                    await telegramBotClient.SendMessage(chat, $"{i + 1}. {listTasks[i].Name} - {listTasks[i].CreatedAt} - {listTasks[i].Id}", cancellationToken: ct);
                }
            }
            else
            {
                await telegramBotClient.SendMessage(chat, " Список задач пуст", cancellationToken: ct);
            }
        }

        private async Task ShowAllTasksAsync(ITelegramBotClient telegramBotClient, Chat chat, long userId, CancellationToken ct)
        {
            var user = await _userService.GetUserAsync(userId, ct);
            if (user == null)
            {
                await telegramBotClient.SendMessage(chat, " Вы не зарегистрированы. Введите \"/start\"", cancellationToken: ct);
                return;
            }
            var listAllTasks = await _todoService.GetAllByUserIdAsync(user!.UserId, ct);

            if (listAllTasks.Count > 0)
            {

                for (int i = 0; i < listAllTasks.Count; i++)
                {
                    if (listAllTasks[i].State == ToDoItem.ToDoItemState.Active)
                        await telegramBotClient.SendMessage(chat, $"{i + 1}. ({ToDoItem.GetStateName(listAllTasks[i].State)}) {listAllTasks[i].Name} - {listAllTasks[i].CreatedAt} - {listAllTasks[i].Id}", cancellationToken: ct);
                    else if (listAllTasks[i].State == ToDoItem.ToDoItemState.Completed)
                        await telegramBotClient.SendMessage(chat, $"{i + 1}. ({ToDoItem.GetStateName(listAllTasks[i].State)}) {listAllTasks[i].Name} - {listAllTasks[i].CreatedAt} - {listAllTasks[i].Id}", cancellationToken: ct);
                }
            }
            else
            {
                await telegramBotClient.SendMessage(chat, " Список задач пуст", cancellationToken: ct);
            }
        }

        private async Task ReportAsync(ITelegramBotClient telegramBotClient, Chat chat, long userId, CancellationToken ct)
        {
            var user = await _userService.GetUserAsync(userId, ct);
            if (user == null)
            {
                await telegramBotClient.SendMessage(chat, " Вы не зарегистрированы. Введите \"/start\"", cancellationToken: ct);
                return;
            }
            var (total, completed, active, generatedAt) = await _toDoReportService.GetUserStatsAsync(user!.UserId, ct);
            string generatedAtStr = generatedAt.ToShortDateString();

            await telegramBotClient.SendMessage(chat, $" Статистика по задачам на {generatedAtStr}", cancellationToken: ct);
            await telegramBotClient.SendMessage(chat, $" Всего: {total}", cancellationToken: ct);
            await telegramBotClient.SendMessage(chat, $" Завершенных: {completed}", cancellationToken: ct);
            await telegramBotClient.SendMessage(chat, $" Активных: {active}", cancellationToken: ct);
        }

        private async Task FindAsync(string namePrefix, ITelegramBotClient telegramBotClient, Chat chat, long userId, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(namePrefix))
            {
                var user = await _userService.GetUserAsync(userId, ct);
                if (user == null)
                {
                    await telegramBotClient.SendMessage(chat, " Вы не зарегистрированы. Введите \"/start\"", cancellationToken: ct);
                    return;
                }
                var listTasks = await _todoService.FindAsync(user!, namePrefix, ct);

                if (listTasks.Count > 0)
                {
                    for (int i = 0; i < listTasks.Count; i++)
                    {
                        await telegramBotClient.SendMessage(chat, $"{i + 1}. {listTasks[i].Name} - {listTasks[i].CreatedAt} - {listTasks[i].Id}", cancellationToken: ct);
                    }
                }
                else
                {
                    await telegramBotClient.SendMessage(chat, " Список задач пуст", cancellationToken: ct);
                }
            }
            else
            {
                await telegramBotClient.SendMessage(chat, " Аргумент для команды \"/find\" отсутствует", cancellationToken: ct);
            }
        }

        private async Task ListUsersAsync(ITelegramBotClient telegramBotClient, Chat chat, CancellationToken ct)
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
                str.AppendLine($" {i + 1}. {users[i].TelegramUserName} | TelegramId: {users[i].TelegramUserId} | State: {users[i].State}");
            }
            await telegramBotClient.SendMessage(chat, str.ToString(), cancellationToken: ct);
        }

        private async Task ShowUserListForStateChangeAsync(ITelegramBotClient telegramBotClient, Chat chat, string callbackPrefix, string prompt, CancellationToken ct)
        {
            var users = await _userService.GetAllUsersAsync(ct);

            if (users.Count == 0)
            {
                await telegramBotClient.SendMessage(chat, " Список пользователей пуст", cancellationToken: ct);
                return;
            }

            var rows = new List<List<InlineKeyboardButton>>();
            foreach (var u in users)
            {
                var buttonText = $"{u.TelegramUserName} ({u.State})";
                var callbackData = $"{callbackPrefix}_{u.UserId}";
                rows.Add(new() { InlineKeyboardButton.WithCallbackData(buttonText, callbackData) });
            }
            rows.Add(new() { InlineKeyboardButton.WithCallbackData("Главное меню", "mainmenu") });

            await telegramBotClient.SendMessage(chat, prompt, replyMarkup: new InlineKeyboardMarkup(rows), cancellationToken: ct);
        }

        private async Task ShowTasksForActionAsync(ITelegramBotClient telegramBotClient, Chat chat, long userId, string action, string prompt, CancellationToken ct)
        {
            var user = await _userService.GetUserAsync(userId, ct);
            if (user == null)
            {
                await telegramBotClient.SendMessage(chat, "Вы не зарегистрированы. Выберите \"Старт\"", cancellationToken: ct);
                return;
            }

            var listTasks = await _todoService.GetActiveByUserIdAsync(user.UserId, ct);
            if (listTasks.Count == 0)
            {
                await telegramBotClient.SendMessage(chat, "Список активных задач пуст", cancellationToken: ct);
                SetState(HandlerState.Ready);
                await SendMainMenuAsync(telegramBotClient, chat, userId, ct);
                return;
            }

            var rows = new List<List<InlineKeyboardButton>>();
            foreach (var task in listTasks)
            {
                var buttonText = task.Name;
                var callbackData = $"{action}_{task.Id}";
                rows.Add(new() { InlineKeyboardButton.WithCallbackData(buttonText, callbackData) });
            }
            rows.Add(new() { InlineKeyboardButton.WithCallbackData("Главное меню", "mainmenu") });

            await telegramBotClient.SendMessage(chat, prompt, replyMarkup: new InlineKeyboardMarkup(rows), cancellationToken: ct);
        }

        private async Task HandleSetStateCallbackAsync(ITelegramBotClient telegramBotClient, Chat chat, string data, long adminUserId, CancellationToken ct)
        {
            var parts = data.Split('_', 3);
            if (parts.Length < 3)
                return;

            string stateName = parts[1];
            if (!Guid.TryParse(parts[2], out Guid targetUserId))
            {
                await telegramBotClient.SendMessage(chat, " Не удалось разобрать идентификатор пользователя", cancellationToken: ct);
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

            await _userService.ChangeStateAsync(targetUserId, targetState, ct);

            var targetUser = await _userService.GetUserByUserIdAsync(targetUserId, ct);
            var userName = targetUser?.TelegramUserName ?? "Unknown";
            await telegramBotClient.SendMessage(chat, $" Пользователь \"{userName}\" теперь имеет статус: {targetState}", cancellationToken: ct);

            var adminUser = await _userService.GetUserAsync(adminUserId, ct);
            await telegramBotClient.SendMessage(chat, " Текущее меню:", replyMarkup: Keyboards.BuildKeyboardForUser(adminUser), cancellationToken: ct);
        }

        private async Task HandleTaskActionCallbackAsync(ITelegramBotClient telegramBotClient, Chat chat, string data, long userId, string action, CancellationToken ct)
        {
            var parts = data.Split('_', 2);
            if (parts.Length < 2)
                return;

            if (!Guid.TryParse(parts[1], out Guid taskId))
            {
                await telegramBotClient.SendMessage(chat, " Не удалось разобрать идентификатор рецепта", cancellationToken: ct);
                return;
            }

            if (action == "info")
            {
                var task = await _todoService.GetTaskAsync(taskId, ct);
                if (task == null)
                {
                    await telegramBotClient.SendMessage(chat, " Задача не найдена", cancellationToken: ct);
                    return;
                }
                var str = new StringBuilder();
                str.AppendLine($" Описание рецепта:");
                str.AppendLine($" Id: {task.Id}");
                str.AppendLine($" Name: {task.Name}");
                str.AppendLine($" CreatedAt: {task.CreatedAt}");
                str.AppendLine($" Deadline: {task.Deadline:dd.MM.yyyy}");
                str.AppendLine($" Category: {ToDoItem.GetCategoryName(task.Category)}");
                str.AppendLine($" SubCategory: {task.SubCategory ?? "-"}");
                str.AppendLine($" Ingredients: {(task.Ingredients != null && task.Ingredients.Count > 0 ? string.Join(", ", task.Ingredients) : "-")}");
                str.AppendLine($" HiddenIngredients: {(task.HiddenIngredients != null && task.HiddenIngredients.Count > 0 ? string.Join(", ", task.HiddenIngredients) : "-")}");
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
                await telegramBotClient.SendMessage(chat, str.ToString(), cancellationToken: ct);
            }
            else if (action == "remove")
            {
                var task = await _todoService.GetTaskAsync(taskId, ct);
                if (task == null)
                {
                    await telegramBotClient.SendMessage(chat, " Задача не найдена", cancellationToken: ct);
                    return;
                }
                await _todoService.DeleteAsync(taskId, ct);
                await telegramBotClient.SendMessage(chat, $@" Задача ""{task.Name}"" удалена", cancellationToken: ct);
            }
            else if (action == "complete")
            {
                var task = await _todoService.GetTaskAsync(taskId, ct);
                if (task == null)
                {
                    await telegramBotClient.SendMessage(chat, " Задача не найдена", cancellationToken: ct);
                    return;
                }
                await _todoService.MarkCompletedAsync(taskId, ct);
                await telegramBotClient.SendMessage(chat, $" Команда с Именем \"{task.Name}\" выполнена", cancellationToken: ct);
            }

            var kbUser = await _userService.GetUserAsync(userId, ct);
            await telegramBotClient.SendMessage(chat, " Текущее меню:", replyMarkup: Keyboards.BuildKeyboardForUser(kbUser), cancellationToken: ct);
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

        private async Task ProcessScenarioAsync(ITelegramBotClient telegramBotClient, Update update, ScenarioContext context, long userId, CancellationToken ct)
        {
            var scenario = GetScenario(context.CurrentScenario);
            if (scenario == null)
            {
                await _contextRepository.ResetContext(userId, ct);
                var chat = update.Message?.Chat ?? update.CallbackQuery?.Message?.Chat;
                if (chat != null)
                {
                    await telegramBotClient.SendMessage(chat, "Сценарий не найден.", replyMarkup: new ReplyKeyboardRemove(), cancellationToken: ct);
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
                await _contextRepository.ResetContext(userId, ct);
                var chat = update.Message?.Chat ?? update.CallbackQuery?.Message?.Chat;
                if (chat != null)
                {
                    await telegramBotClient.SendMessage(chat, $"Ошибка при выполнении сценария: {ex.Message}", replyMarkup: new ReplyKeyboardRemove(), cancellationToken: ct);
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
                    await telegramBotClient.SendMessage(chat, "✅", replyMarkup: new ReplyKeyboardRemove(), cancellationToken: ct);
                    await SendMainMenuAsync(telegramBotClient, chat, userId, ct);
                }
            }
            else
            {
                await _contextRepository.SetContext(userId, context, ct);
            }
        }
    }
}
