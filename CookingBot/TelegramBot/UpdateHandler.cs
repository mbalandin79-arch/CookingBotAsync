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
            Ready
        }

        private readonly IUserService _userService;
        private readonly IToDoService _todoService;
        private readonly IToDoReportService _toDoReportService;

        private HandlerState _state = HandlerState.Init;
        private string _displayName = "Гость";
        private int _maxTask = 0;
        private int _maxLengthTask = 0;
        private readonly object _stateSync = new object();
        private readonly SemaphoreSlim _handlelock = new SemaphoreSlim(1, 1);

        public UpdateHandler(IUserService userService, IToDoService todoService, IToDoReportService toDoReportService)
        {
            _userService = userService;
            _todoService = todoService;
            _toDoReportService = toDoReportService;
        }

        public Task HandleErrorAsync(ITelegramBotClient telegramBotClient, Exception exception, HandleErrorSource source, CancellationToken ct)
        {
            Console.WriteLine($"HandleError ({source}): {exception})");
            return Task.CompletedTask;
        }

        public async Task HandleUpdateAsync(ITelegramBotClient telegramBotClient, Update update, CancellationToken ct)
        {
            await _handlelock.WaitAsync();

            try
            {
                if (update.Message?.From == null || update.Message.Text == null)
                    return;

                long userId = update.Message.From.Id;
                var text = update.Message.Text;

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
                                await telegramBotClient.SendMessage(update.Message!.Chat,
                                        " Конфигурация принята. Введите \"/start\" для начала работы.", cancellationToken: ct);
                                SetState(HandlerState.AwaitingStart);
                            }
                            catch (ArgumentException e)
                            {
                                await telegramBotClient.SendMessage(update.Message!.Chat, $"{e.Message}. Попробуйте еще раз (1-100)", cancellationToken: ct);
                            }
                            break;
                        case HandlerState.AwaitingStart:
                            await MyNameIsAsync(telegramBotClient, update, text, userId, ct);
                            break;
                        case HandlerState.AwaitingRegistration:
                            if (text.ToLower().Trim() == "y")
                            {
                                await telegramBotClient.SendMessage(update.Message!.Chat, $" Ваше отображаемое Имя \"{update.Message.From.Username}\" ", cancellationToken: ct);
                                await telegramBotClient.SendMessage(update.Message!.Chat, " Если хотите изменить, введите новое Имя. Если нет, просто нажмите Enter ", cancellationToken: ct);
                                SetState(HandlerState.AwaitingRegistrationName);
                            }
                            else
                            {
                                await telegramBotClient.SendMessage(update.Message!.Chat, " Регистрация отменена. Для начала работы введите \"/start\"", cancellationToken: ct);
                                SetState(HandlerState.AwaitingStart);
                            }
                            break;
                        case HandlerState.AwaitingRegistrationName:
                            await UserRegistrationAsync(telegramBotClient, update, text, userId, ct);
                            SetState(HandlerState.Ready);
                            await telegramBotClient.SendMessage(update.Message!.Chat, $"{_displayName}, Введите команду: ", cancellationToken: ct);
                            break;
                        case HandlerState.Ready:
                            await WorkAsync(telegramBotClient, update, text, userId, ct);
                            break;
                    }
                }
                catch (OperationCanceledException)
                {

                }
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

        private async Task GreetingAsync(ITelegramBotClient telegramBotClient, Update update, CancellationToken ct)
        {
            var str = new StringBuilder("\n");
            str.AppendLine(" Приветствую Вас в проекте \"Кулинарный бот\"\n");
            str.AppendLine(" Бот поддерживает следующие команды при старте:");
            str.AppendLine(" \"/start\" - используется для начала работы");
            str.AppendLine(" \"/help\" - отображает краткую информацию как пользоваться Ботом, также выводит список доступных команд во время работы");
            str.AppendLine(" \"/info\" - предоставляет информацию о версии программы и дате её создания");
            str.AppendLine(" \"/myinfo\" - отображает краткую информацию о самом пользователе и его статусе");
            str.AppendLine(" \"/addtask Задача\" - позволяет добавить Задачу, между командой и Задачей обязательно должен быть пробел");
            //str.AppendLine(" \"/edittask Идентификатор\" - позволяет заполнить по Content у Задачу по ее Идентификатору, между командой и Идентификатором обязательно должен быть пробел");
            str.AppendLine(" \"/showtasks\" - отображает все \"Активные\" задачи");
            str.AppendLine(" \"/showalltasks\" - отображает все задачи");
            str.AppendLine(" \"/infotask Идентификатор\" - отображает информацию о задачи по ее Идентификатору, между командой и Идентификатором обязательно должен быть пробел");
            str.AppendLine(" \"/removetask Идентификатор\" - позволяет удалить доступную задачу по ее Идентификатору, между командой и Идентификатором обязательно должен быть пробел");
            str.AppendLine(" \"/completetask Идентификатор\" - позволяет изменить состояние задачи с \"Активная\" на \"Завершенная\", между командой и Идентификатором обязательно должен быть пробел");
            str.AppendLine(" \"/report\" - отображает статистику по задачам текущего пользователя на данный момент времени");
            str.AppendLine(" \"/find Имя\" - отображает все задачи зарегистрированного пользователя с именем \"Имя\", между командой и Именем обязательно должен быть пробел");
            str.AppendLine(" \"/exit\" - завершение работы\n");
            str.AppendLine(" В процессе работы перечень доступных команд будет меняться");
            str.AppendLine(" Команды следует вводить с клавиатуры в Консоль");
            str.AppendLine(" Окончанием ввода команды считается нажатие клавиши Enter");
            str.AppendLine(" Некоторым командам потребуются дополнительные данные, об этом будет указано в описании команды\n");
            await telegramBotClient.SendMessage(update.Message!.Chat, str.ToString(), cancellationToken: ct);
        }

        private async Task WorkAsync(ITelegramBotClient telegramBotClient, Update update, string text, long userId, CancellationToken ct)
        {
            var inputStr = text.ToLower();
            var command = inputStr.Split(' ')[0];

            switch (command)
            {
                case "/start":
                    var user = await _userService.GetUserAsync(userId, ct);
                    if (user != null)
                    {
                        _displayName = user.TelegramUserName;
                        await telegramBotClient.SendMessage(update.Message!.Chat, $" {_displayName} Добро пожаловать", cancellationToken: ct);
                    }
                    break;
                case "/help":
                    await HelpAsync(telegramBotClient, update, userId, ct);
                    break;
                case "/info":
                    await InfoAsync(telegramBotClient, update, ct);
                    break;
                case "/myinfo":
                    await MyInfoAsync(telegramBotClient, update, userId, ct);
                    break;
                case "/addtask":
                    await AddTaskAsync(inputStr, telegramBotClient, update, userId, ct);
                    break;
                //case "/edittask":
                //    await EditTaskAsync(inputStr, telegramBotClient, update, ct);
                //    break;
                case "/showtasks":
                    await ShowTasksAsync(telegramBotClient, update, userId, ct);
                    break;
                case "/showalltasks":
                    await ShowAllTasksAsync(telegramBotClient, update, userId, ct);
                    break;
                case "/infotask":
                    await InfoTaskAsync(inputStr, telegramBotClient, update, ct);
                    break;
                case "/removetask":
                    await RemoveTaskAsync(inputStr, telegramBotClient, update, userId, ct);
                    break;
                case "/completetask":
                    await CompleteTaskAsync(inputStr, telegramBotClient, update, userId, ct);
                    break;
                case "/report":
                    await ReportAsync(telegramBotClient, update, userId, ct);
                    break;
                case "/find":
                    await FindAsync(inputStr, telegramBotClient, update, userId, ct);
                    break;
                case "/exit":
                    Environment.Exit(0);
                    break;
                default:
                    await telegramBotClient.SendMessage(update.Message!.Chat, " Бот не знает такой команды либо эта команда недоступна\n Для просмотра доступных команд введите \"/help\"", cancellationToken: ct);
                    break;
            }

            if (GetState() == HandlerState.Ready)
                await telegramBotClient.SendMessage(update.Message!.Chat, $"{_displayName}, Введите команду: ", cancellationToken: ct);
        }

        private async Task InfoTaskAsync(string inputStr, ITelegramBotClient telegramBotClient, Update update, CancellationToken ct)
        {
            var parts = inputStr.Split(' ', 2);
            string selectedId = parts.Length > 1 ? parts[1] : string.Empty;

            if (Guid.TryParse(selectedId, out Guid num))
            {
                var task = await _todoService.GetTaskAsync(num, ct);

                if (task == null)
                {
                    await telegramBotClient.SendMessage(update.Message!.Chat, " Задачи с таким номером нет, попробуйте еще раз: ", cancellationToken: ct);
                }
                else
                {
                    var str = new StringBuilder();
                    str.AppendLine($" Описание задачи:");
                    str.AppendLine($" Id: {task.Id}");
                    str.AppendLine($" User:\tUserId: {task.User.UserId}");
                    str.AppendLine($" \t\tTelegramUserId: {task.User.TelegramUserId}");
                    str.AppendLine($" \t\tTelegramUserName: {task.User.TelegramUserName}");
                    str.AppendLine($" \t\tRegistered Date: {task.User.RegisteredAt}");
                    str.AppendLine($" Name: {task.Name}");
                    str.AppendLine($" CreatedAt: {task.CreatedAt}");
                    str.AppendLine($" Content: {task.Content ?? string.Empty}");
                    str.AppendLine($" State: {task.State}");
                    str.AppendLine($" StateChangedAt: {task.StateChangedAt}");
                    await telegramBotClient.SendMessage(update.Message!.Chat, str.ToString(), cancellationToken: ct);
                }
            }
            else
            {
                await telegramBotClient.SendMessage(update.Message!.Chat, " Необходимо ввести именно Идентификатор задачи, попробуйте еще раз: ", cancellationToken: ct);
            }
        }

        private async Task MyInfoAsync(ITelegramBotClient telegramBotClient, Update update, long userId, CancellationToken ct)
        {
            var myUser = await _userService.GetUserAsync(userId, ct);

            if (myUser != null)
            {
                await telegramBotClient.SendMessage(update.Message!.Chat, " Ваши регистрационные данные:", cancellationToken: ct);
                await telegramBotClient.SendMessage(update.Message!.Chat, $" UserId: {myUser.UserId}", cancellationToken: ct);
                await telegramBotClient.SendMessage(update.Message!.Chat, $" TelegramUserId: {myUser.TelegramUserId}", cancellationToken: ct);
                await telegramBotClient.SendMessage(update.Message!.Chat, $" TelegramUserName: {myUser.TelegramUserName}", cancellationToken: ct);
                await telegramBotClient.SendMessage(update.Message!.Chat, $" Registered Date: {myUser.RegisteredAt}", cancellationToken: ct);
                await telegramBotClient.SendMessage(update.Message!.Chat, $" State: {myUser.State}", cancellationToken: ct);
            }
            else
            {
                await telegramBotClient.SendMessage(update.Message!.Chat, " Вы не зарегистрированы", cancellationToken: ct);
            }
        }

        private async Task UserRegistrationAsync(ITelegramBotClient telegramBotClient, Update update, string text, long userId, CancellationToken ct)
        {
            string telegramUserName = string.IsNullOrWhiteSpace(text) ? (update.Message!.From.Username ?? $"User_{userId}") : text;

            var newUser = await _userService.RegisterUserAsync(userId, telegramUserName, ct);

            await telegramBotClient.SendMessage(update.Message!.Chat, " Зарегистрирован новый Пользователь", cancellationToken: ct);
            await telegramBotClient.SendMessage(update.Message!.Chat, $" UserId: {newUser.UserId}", cancellationToken: ct);
            await telegramBotClient.SendMessage(update.Message!.Chat, $" TelegramUserId: {newUser.TelegramUserId}", cancellationToken: ct);
            await telegramBotClient.SendMessage(update.Message!.Chat, $" TelegramUserName: {newUser.TelegramUserName}", cancellationToken: ct);
            await telegramBotClient.SendMessage(update.Message!.Chat, $" Registered Date: {newUser.RegisteredAt}", cancellationToken: ct);
            await telegramBotClient.SendMessage(update.Message!.Chat, $" State: {newUser.State}", cancellationToken: ct);
            _displayName = newUser.TelegramUserName;
        }

        private async Task MyNameIsAsync(ITelegramBotClient telegramBotClient, Update update, string text, long userId, CancellationToken ct)
        {
            var command = text.ToLower().Split(' ')[0];

            if (command == "/start")
            {
                ToDoUser? user = await _userService.GetUserAsync(userId, ct);

                if (user == null)
                {
                    await telegramBotClient.SendMessage(update.Message!.Chat, " Вы еще не зарегистрированы. Хотите принять участие в проекте \"Кулинарный Бот\"?", cancellationToken: ct);
                    await telegramBotClient.SendMessage(update.Message!.Chat, " Для регистрации введите \"Y\" ", cancellationToken: ct);
                    SetState(HandlerState.AwaitingRegistration);
                }
                else
                {
                    _displayName = user.TelegramUserName;
                    await telegramBotClient.SendMessage(update.Message!.Chat, $" {user.TelegramUserName} Добро пожаловать", cancellationToken: ct);
                    SetState(HandlerState.Ready);
                    await telegramBotClient.SendMessage(update.Message!.Chat, $"{_displayName}, Введите команду: ", cancellationToken: ct);
                }
            }
            else if (command == "/help")
            {
                await HelpAsync(telegramBotClient, update, userId, ct);
            }
            else if (command == "/info")
            {
                await InfoAsync(telegramBotClient, update, ct);
            }
            else
            {
                await telegramBotClient.SendMessage(update.Message!.Chat, " Бот не знает такой команды либо эта команда недоступна\n Для начала работы введите \"/start\"", cancellationToken: ct);
            }
        }

        private async Task HelpAsync(ITelegramBotClient telegramBotClient, Update update, long userId, CancellationToken ct)
        {
            var user = await _userService.GetUserAsync(userId, ct);

            var str = new StringBuilder("\n"); ;
            str.AppendLine(" Вам доступны следующие команды:");
            if (user == null)
            {
                str.AppendLine(" \"/start\" - используется для начала работы");
            }
            str.AppendLine(" \"/help\" - отображает краткую информацию как пользоваться Ботом, также выводит список доступных команд во время работы");
            str.AppendLine(" \"/info\" - предоставляет информацию о версии программы и дате её создания");
            if (user != null)
            {
                str.AppendLine(" \"/myinfo\" - отображает краткую информацию о самом пользователе и его статусе");
                str.AppendLine(" \"/addtask Задача\" - позволяет добавить Задачу, между командой и Задачей обязательно должен быть пробел");
                //str.AppendLine(" \"/edittask Идентификатор\" - позволяет заполнить по Content у Задачу по ее Идентификатору, между командой и Идентификатором обязательно должен быть пробел");
                str.AppendLine(" \"/showtasks\" - отображает все \"Активные\" задачи");
                str.AppendLine(" \"/showalltasks\" - отображает все задачи");
                str.AppendLine(" \"/infotask Идентификатор\" - отображает информацию о задачи по ее Идентификатору, между командой и Идентификатором обязательно должен быть пробел");
                str.AppendLine(" \"/removetask Идентификатор\" - позволяет удалить доступную задачу по ее Идентификатору, между командой и Идентификатором обязательно должен быть пробел");
                str.AppendLine(" \"/completetask Идентификатор\" - позволяет изменить состояние задачи с \"Активная\" на \"Завершенная\", между командой и Идентификатором обязательно должен быть пробел");
                str.AppendLine(" \"/report\" - отображает статистику по задачам текущего пользователя на данный момент времени");
                str.AppendLine(" \"/find Имя\" - отображает все задачи зарегистрированного пользователя с именем \"Имя\", между командой и Именем обязательно должен быть пробел");
            }
            str.AppendLine(" \"/exit\" - завершает работу Бота\n");
            await telegramBotClient.SendMessage(update.Message!.Chat, str.ToString(), cancellationToken: ct);
        }

        private async Task InfoAsync(ITelegramBotClient telegramBotClient, Update update, CancellationToken ct)
        {
            string createDate = " Created 21.05.2026    ";

            Assembly assembly = Assembly.GetExecutingAssembly();
            AssemblyName assemblyName = assembly.GetName();
            Version version = assemblyName.Version!;

            await telegramBotClient.SendMessage(update.Message!.Chat, $"{createDate} The Version used {version}", cancellationToken: ct);
        }

        private async Task AddTaskAsync(string inputStr, ITelegramBotClient telegramBotClient, Update update, long userId, CancellationToken ct)
        {
            try
            {
                var parts = inputStr.Split(' ', 2);
                string newTask = parts.Length > 1 ? parts[1] : string.Empty;

                if (!string.IsNullOrWhiteSpace(newTask))
                {
                    var user = await _userService.GetUserAsync(userId, ct);
                    if (user == null)
                    {
                        await telegramBotClient.SendMessage(update.Message!.Chat, " Вы не зарегистрированы. Введите \"/start\"", cancellationToken: ct);
                        return;
                    }
                    var newToDoItem = await _todoService.AddAsync(user!, newTask, ct);

                    var str = new StringBuilder();
                    str.AppendLine($" Задача добавлена:");
                    str.AppendLine($" Id: {newToDoItem.Id}");
                    str.AppendLine($" User:\tUserId: {newToDoItem.User.UserId}");
                    str.AppendLine($" \t\tTelegramUserId: {newToDoItem.User.TelegramUserId}");
                    str.AppendLine($" \t\tTelegramUserName: {newToDoItem.User.TelegramUserName}");
                    str.AppendLine($" \t\tRegistered Date: {newToDoItem.User.RegisteredAt}");
                    str.AppendLine($" Name: {newToDoItem.Name}");
                    str.AppendLine($" CreatedAt: {newToDoItem.CreatedAt}");
                    str.AppendLine($" Content: {newToDoItem.Content ?? string.Empty}");
                    str.AppendLine($" State: {newToDoItem.State}");
                    str.AppendLine($" StateChangedAt: {newToDoItem.StateChangedAt}");
                    await telegramBotClient.SendMessage(update.Message!.Chat, str.ToString(), cancellationToken: ct);
                }
                else
                {
                    await telegramBotClient.SendMessage(update.Message!.Chat, " Аргумент для команды \"/addtask\" отсутствует", cancellationToken: ct);
                }
            }
            catch (TaskCountLimitException e)
            {
                await telegramBotClient.SendMessage(update.Message!.Chat, $"Превышено максимальное количество задач равное {e.TaskCountLimit}", cancellationToken: ct);
            }
            catch (TaskLengthLimitException e)
            {
                await telegramBotClient.SendMessage(update.Message!.Chat, $"Длина задачи ‘{e.TaskLength}’ превышает максимально допустимое значение {e.TaskLengthLimit}", cancellationToken: ct);
            }
            catch (DuplicateTaskException e)
            {
                await telegramBotClient.SendMessage(update.Message!.Chat, $"Задача ‘{e.Task}’ уже существует", cancellationToken: ct);
            }
            catch (ArgumentException e)
            {
                await telegramBotClient.SendMessage(update.Message!.Chat, e.Message, cancellationToken: ct);
            }
        }

        private async Task ShowTasksAsync(ITelegramBotClient telegramBotClient, Update update, long userId, CancellationToken ct)
        {
            var user = await _userService.GetUserAsync(userId, ct);
            if (user == null)
            {
                await telegramBotClient.SendMessage(update.Message!.Chat, " Вы не зарегистрированы. Введите \"/start\"", cancellationToken: ct);
                return;
            }
            var listTasks = await _todoService.GetActiveByUserIdAsync(user!.UserId, ct);

            if (listTasks.Count > 0)
            {
                for (int i = 0; i < listTasks.Count; i++)
                {
                    await telegramBotClient.SendMessage(update.Message!.Chat, $"{i + 1}. {listTasks[i].Name} - {listTasks[i].CreatedAt} - {listTasks[i].Id}", cancellationToken: ct);
                }
            }
            else
            {
                await telegramBotClient.SendMessage(update.Message!.Chat, " Список задач пуст", cancellationToken: ct);
            }
        }

        private async Task ShowAllTasksAsync(ITelegramBotClient telegramBotClient, Update update, long userId, CancellationToken ct)
        {
            var user = await _userService.GetUserAsync(userId, ct);
            if (user == null)
            {
                await telegramBotClient.SendMessage(update.Message!.Chat, " Вы не зарегистрированы. Введите \"/start\"", cancellationToken: ct);
                return;
            }
            var listAllTasks = await _todoService.GetAllByUserIdAsync(user!.UserId, ct);

            if (listAllTasks.Count > 0)
            {

                for (int i = 0; i < listAllTasks.Count; i++)
                {
                    if (listAllTasks[i].State == ToDoItem.ToDoItemState.Active)
                        await telegramBotClient.SendMessage(update.Message!.Chat, $"{i + 1}. (Active) {listAllTasks[i].Name} - {listAllTasks[i].CreatedAt} - {listAllTasks[i].Id}", cancellationToken: ct);
                    else if (listAllTasks[i].State == ToDoItem.ToDoItemState.Completed)
                        await telegramBotClient.SendMessage(update.Message!.Chat, $"{i + 1}. (Complete) {listAllTasks[i].Name} - {listAllTasks[i].CreatedAt} - {listAllTasks[i].Id}", cancellationToken: ct);
                }
            }
            else
            {
                await telegramBotClient.SendMessage(update.Message!.Chat, " Список задач пуст", cancellationToken: ct);
            }
        }

        private async Task CompleteTaskAsync(string inputStr, ITelegramBotClient telegramBotClient, Update update, long userId, CancellationToken ct)
        {
            var user = await _userService.GetUserAsync(userId, ct);
            if (user == null)
            {
                await telegramBotClient.SendMessage(update.Message!.Chat, " Вы не зарегистрированы. Введите \"/start\"", cancellationToken: ct);
                return;
            }
            var listTasks = await _todoService.GetActiveByUserIdAsync(user!.UserId, ct);

            if (listTasks.Count > 0)
            {
                var parts = inputStr.Split(' ', 2);
                string selectedId = parts.Length > 1 ? parts[1] : string.Empty;

                if (Guid.TryParse(selectedId, out Guid num))
                {
                    var target = listTasks.Where(w => w.Id == num).FirstOrDefault();
                    if (target == null)
                    {
                        await telegramBotClient.SendMessage(update.Message!.Chat, " Задачи с таким номером нет, попробуйте еще раз: ", cancellationToken: ct);
                    }
                    else
                    {
                        await _todoService.MarkCompletedAsync(num, ct);
                        await telegramBotClient.SendMessage(update.Message!.Chat, $" Команда с Именем \"{target.Name}\" выполнена", cancellationToken: ct);
                    }
                }
                else
                {
                    await telegramBotClient.SendMessage(update.Message!.Chat, " Необходимо ввести именно Идентификатор задачи, попробуйте еще раз: ", cancellationToken: ct);
                }
            }
            else
            {
                await telegramBotClient.SendMessage(update.Message!.Chat, " Список задач пуст", cancellationToken: ct);
            }
        }

        private async Task RemoveTaskAsync(string inputStr, ITelegramBotClient telegramBotClient, Update update, long userId, CancellationToken ct)
        {
            var user = await _userService.GetUserAsync(userId, ct);
            if (user == null)
            {
                await telegramBotClient.SendMessage(update.Message!.Chat, " Вы не зарегистрированы. Введите \"/start\"", cancellationToken: ct);
                return;
            }
            var listAllTasks = await _todoService.GetAllByUserIdAsync(user!.UserId, ct);

            if (listAllTasks.Count > 0)
            {
                var parts = inputStr.Split(' ', 2);
                string selectedId = parts.Length > 1 ? parts[1] : string.Empty;

                if (Guid.TryParse(selectedId, out Guid num))
                {
                    var target = listAllTasks.FirstOrDefault(w => w.Id == num);
                    if (target == null)
                    {
                        await telegramBotClient.SendMessage(update.Message!.Chat, " Задачи с таким номером нет, попробуйте еще раз: ", cancellationToken: ct);
                    }
                    else
                    {
                        await telegramBotClient.SendMessage(update.Message!.Chat, $@" Задача ""{target.Name}"" удалена", cancellationToken: ct);
                        await _todoService.DeleteAsync(num, ct);
                    }
                }
                else
                {
                    await telegramBotClient.SendMessage(update.Message!.Chat, " Необходимо ввести именно Идентификатор задачи, попробуйте еще раз: ", cancellationToken: ct);
                }
                await ShowAllTasksAsync(telegramBotClient, update, userId, ct);
            }
            else
            {
                await telegramBotClient.SendMessage(update.Message!.Chat, " Список задач пуст", cancellationToken: ct);
            }
        }

        private async Task ReportAsync(ITelegramBotClient telegramBotClient, Update update, long userId, CancellationToken ct)
        {
            var user = await _userService.GetUserAsync(userId, ct);
            if (user == null)
            {
                await telegramBotClient.SendMessage(update.Message!.Chat, " Вы не зарегистрированы. Введите \"/start\"", cancellationToken: ct);
                return;
            }
            var (total, completed, active, generatedAt) = await _toDoReportService.GetUserStatsAsync(user!.UserId, ct);
            string generatedAtStr = generatedAt.ToShortDateString();

            await telegramBotClient.SendMessage(update.Message!.Chat, $" Статистика по задачам на {generatedAtStr}", cancellationToken: ct);
            await telegramBotClient.SendMessage(update.Message!.Chat, $" Всего: {total}", cancellationToken: ct);
            await telegramBotClient.SendMessage(update.Message!.Chat, $" Завершенных: {completed}", cancellationToken: ct);
            await telegramBotClient.SendMessage(update.Message!.Chat, $" Активных: {active}", cancellationToken: ct);
        }

        private async Task FindAsync(string inputStr, ITelegramBotClient telegramBotClient, Update update, long userId, CancellationToken ct)
        {
            var parts = inputStr.Split(' ', 2);
            string namePrefix = parts.Length > 1 ? parts[1] : string.Empty;

            if (!string.IsNullOrWhiteSpace(namePrefix))
            {
                var user = await _userService.GetUserAsync(userId, ct);
                if (user == null)
                {
                    await telegramBotClient.SendMessage(update.Message!.Chat, " Вы не зарегистрированы. Введите \"/start\"", cancellationToken: ct);
                    return;
                }
                var listTasks = await _todoService.FindAsync(user!, namePrefix, ct);

                if (listTasks.Count > 0)
                {
                    for (int i = 0; i < listTasks.Count; i++)
                    {
                        await telegramBotClient.SendMessage(update.Message!.Chat, $"{i + 1}. {listTasks[i].Name} - {listTasks[i].CreatedAt} - {listTasks[i].Id}", cancellationToken: ct);
                    }
                }
                else
                {
                    await telegramBotClient.SendMessage(update.Message!.Chat, " Список задач пуст", cancellationToken: ct);
                }
            }
            else
            {
                await telegramBotClient.SendMessage(update.Message!.Chat, " Аргумент для команды \"/find\" отсутствует", cancellationToken: ct);
            }
        }
    }
}
