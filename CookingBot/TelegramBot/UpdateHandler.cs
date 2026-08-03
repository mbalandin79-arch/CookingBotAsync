using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using CookingBot.Core.Entities;
using CookingBot.Core.Exceptions;
using CookingBot.Core.Services;
using Otus.ToDoList.ConsoleBot;
using Otus.ToDoList.ConsoleBot.Types;

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
        private string displayName = "Гость";
        private int maxTask = 0;
        private int maxLengthTask = 0;
        private long _userId;

        public UpdateHandler(IUserService userService, IToDoService todoService, IToDoReportService toDoReportService)
        {
            _userService = userService;
            _todoService = todoService;
            _toDoReportService = toDoReportService;
        }

        public Task HandleErrorAsync(ITelegramBotClient telegramBotClient, Exception exception, CancellationToken ct)
        {
            Console.WriteLine($"HandleError: {exception})");
            return Task.CompletedTask;
        }

        public async Task HandleUpdateAsync(ITelegramBotClient telegramBotClient, Update update, CancellationToken ct)
        {
            _userId = update.Message.From.Id;
            var text = update.Message.Text ?? string.Empty;

            try
            {
                switch (_state)
                {
                    case HandlerState.Init:
                        await telegramBotClient.SendMessage(update.Message.Chat, $"Получил '{update.Message.Text}'", ct);
                        await GreetingAsync(telegramBotClient, update, ct);
                        await telegramBotClient.SendMessage(update.Message.Chat, " Для начала введите максимально допустимое количество задач в диапазоне от 1 до 100: ", ct);
                        _state = HandlerState.AwaitingMaxTasks;
                        break;
                    case HandlerState.AwaitingMaxTasks:
                        try
                        {
                            if (string.IsNullOrWhiteSpace(text))
                                throw new ArgumentException("Значение не соответствует требованиям");
                            maxTask = ParseAndValidateInt(text, 1, 100);
                            await telegramBotClient.SendMessage(update.Message.Chat, " А теперь введите максимально допустимую длину задачи в диапазоне от 1 до 100: ", ct);
                            _state = HandlerState.AwaitingMaxLength;
                        }
                        catch (ArgumentException e)
                        {
                            await telegramBotClient.SendMessage(update.Message.Chat, $"{e.Message}. Попробуйте еще раз (1-100)", ct);
                        }
                        break;
                    case HandlerState.AwaitingMaxLength:
                        try
                        {
                            if (string.IsNullOrWhiteSpace(text))
                                throw new ArgumentException("Значение не соответствует требованиям");
                            maxLengthTask = ParseAndValidateInt(text, 1, 100);
                            await _todoService.SetConfigurationAsync(maxTask, maxLengthTask);
                            await telegramBotClient.SendMessage(update.Message.Chat,
                                    " Конфигурация принята. Введите \"/start\" для начала работы.", ct);
                            _state = HandlerState.AwaitingStart;
                        }
                        catch (ArgumentException e)
                        {
                            await telegramBotClient.SendMessage(update.Message.Chat, $"{e.Message}. Попробуйте еще раз (1-100)", ct);
                        }
                        break;
                    case HandlerState.AwaitingStart:
                        await MyNameIsAsync(telegramBotClient, update, text, ct);
                        break;
                    case HandlerState.AwaitingRegistration:
                        if (text.ToLower().Trim() == "y")
                        {
                            await telegramBotClient.SendMessage(update.Message.Chat, $" Ваше отображаемое Имя \"{update.Message.From.Username}\" ", ct);
                            await telegramBotClient.SendMessage(update.Message.Chat, " Если хотите изменить, введите новое Имя. Если нет, просто нажмите Enter ", ct);
                            _state = HandlerState.AwaitingRegistrationName;
                        }
                        else
                        {
                            await telegramBotClient.SendMessage(update.Message.Chat, " Регистрация отменена. Для начала работы введите \"/start\"", ct);
                            _state = HandlerState.AwaitingStart;
                        }
                        break;
                    case HandlerState.AwaitingRegistrationName:
                        await UserRegistrationAsync(telegramBotClient, update, text, ct);
                        _state = HandlerState.Ready;
                        await telegramBotClient.SendMessage(update.Message.Chat, $"{displayName}, Введите команду: ", ct);
                        break;
                    case HandlerState.Ready:
                        await WorkAsync(telegramBotClient, update, text, ct);
                        break;
                }
            }
            catch (Exception ex)
            {
                await telegramBotClient.SendMessage(update.Message.Chat, $" Произошла непредвиденная ошибка:\n Тип ошибки: {ex.GetType().Name}\n Сообщение: {ex.Message}", ct);
                var trace = new StackTrace(ex, true);
                foreach (var item in trace.GetFrames())
                {
                    await telegramBotClient.SendMessage(update.Message.Chat, $"Файл: {item.GetFileName()}, Строка: {item.GetFileLineNumber()}, Метод: {item.GetMethod()}", ct);
                }
                if (ex.InnerException != null)
                {
                    await telegramBotClient.SendMessage(update.Message.Chat, $" Внутреннее исключение:\n Тип: {ex.InnerException.GetType().Name}\n Сообщение: {ex.InnerException.Message}\n", ct);
                    var newtrace = new StackTrace(ex.InnerException, true);
                    foreach (var item in newtrace.GetFrames())
                    {
                        await telegramBotClient.SendMessage(update.Message.Chat, $"Файл: {item.GetFileName()}, Строка: {item.GetFileLineNumber()}, Метод: {item.GetMethod()}", ct);
                    }
                }
            }
        }

        private void ValidateString(string? str)
        {
            if (string.IsNullOrWhiteSpace(str))
                throw new ArgumentException($"{str} это значение не соответствует требованиям");
        }

        private int ParseAndValidateInt(string? str, int min, int max)
        {
            int answ = 0;

            if (!int.TryParse(str, out answ) || answ < min || answ > max)
                throw new ArgumentException($"{str} это значение не соответствует требованиям");
            return answ;
        }

        private async Task GreetingAsync(ITelegramBotClient telegramBotClient, Update update, CancellationToken ct)
        {
            var str = new StringBuilder("\n");
            str.AppendLine(" Приветствую Вас в проекте \"Кулинарный бот\"\n");
            str.AppendLine(" Бот поддерживает следующие команды при старте:");
            str.AppendLine(" \"/start\" - используется для начала работы");
            str.AppendLine(" \"/help\" - отображает краткую информацию как пользоваться Ботом, также выводит список доступных команд во время работы");
            str.AppendLine(" \"/info\" - предоставляет информацию о версии программы и дате её создания");
            str.AppendLine(" \"/addtask Задача\" - позволяет добавить Задачу, между командой и Задачей обязательно должен быть пробел");
            str.AppendLine(" \"/showtasks\" - отображает все \"Активные\" задачи");
            str.AppendLine(" \"/showalltasks\" - отображает все задачи");
            str.AppendLine(" \"/removetask Идентификатор\" - позволяет удалить доступную задачу по ее Идентификатору, между командой и Идентификатором обязательно должен быть пробел");
            str.AppendLine(" \"/completetask Идентификатор\" - позволяет изменить состояние задачи с \"Активная\" на \"Завершенная\", между командой и Идентификатором обязательно должен быть пробел");
            str.AppendLine(" \"/report\" - отображает статистику по задачам текущего пользователя на данный момент времени");
            str.AppendLine(" \"/find Имя\" - отображает все задачи зарегистрированного пользователя с именем \"Имя\", между командой и Именем обязательно должен быть пробел");
            str.AppendLine(" \"/exit\" - завершение работы\n");
            str.AppendLine(" В процессе работы перечень доступных команд будет меняться");
            str.AppendLine(" Команды следует вводить с клавиатуры в Консоль");
            str.AppendLine(" Окончанием ввода команды считается нажатие клавиши Enter");
            str.AppendLine(" Некоторым командам потребуются дополнительные данные, об этом будет указано в описании команды\n");
            await telegramBotClient.SendMessage(update.Message.Chat, str.ToString(), ct);
        }

        private async Task WorkAsync(ITelegramBotClient telegramBotClient, Update update, string text, CancellationToken ct)
        {
            var inputStr = text.ToLower();
            var command = inputStr.Split(' ')[0];

            switch (command)
            {
                case "/start":
                    var user = await _userService.GetUserAsync(_userId);
                    if (user != null)
                    {
                        displayName = user.TelegramUserName;
                        await telegramBotClient.SendMessage(update.Message.Chat, $" {displayName} Добро пожаловать", ct);
                    }
                    break;
                case "/help":
                    await HelpAsync(telegramBotClient, update, ct);
                    break;
                case "/info":
                    await InfoAsync(telegramBotClient, update, ct);
                    break;
                case "/addtask":
                    await AddTaskAsync(inputStr, telegramBotClient, update, ct);
                    break;
                case "/showtasks":
                    await ShowTasksAsync(telegramBotClient, update, ct);
                    break;
                case "/showalltasks":
                    await ShowAllTasksAsync(telegramBotClient, update, ct);
                    break;
                case "/removetask":
                    await RemoveTaskAsync(inputStr, telegramBotClient, update, ct);
                    break;
                case "/completetask":
                    await CompleteTaskAsync(inputStr, telegramBotClient, update, ct);
                    break;
                case "/report":
                    await ReportAsync(telegramBotClient, update, ct);
                    break;
                case "/find":
                    await FindAsync(inputStr, telegramBotClient, update, ct);
                    break;
                case "/exit":
                    Environment.Exit(0);
                    break;
                default:
                    await telegramBotClient.SendMessage(update.Message.Chat, " Бот не знает такой команды либо эта команда недоступна\n Для просмотра доступных команд введите \"/help\"", ct);
                    break;
            }

            if (_state == HandlerState.Ready)
                await telegramBotClient.SendMessage(update.Message.Chat, $"{displayName}, Введите команду: ", ct);
        }

        private async Task UserRegistrationAsync(ITelegramBotClient telegramBotClient, Update update, string text, CancellationToken ct)
        {
            string telegramUserName = string.IsNullOrWhiteSpace(text) ? update.Message.From.Username! : text;

            ToDoUser newUser = await _userService.RegisterUserAsync(_userId, telegramUserName);

            await telegramBotClient.SendMessage(update.Message.Chat, " Зарегистрирован новый Пользователь", ct);
            await telegramBotClient.SendMessage(update.Message.Chat, $" UserId: {newUser.UserId}", ct);
            await telegramBotClient.SendMessage(update.Message.Chat, $" TelegramUserId: {newUser.TelegramUserId}", ct);
            await telegramBotClient.SendMessage(update.Message.Chat, $" TelegramUserName: {newUser.TelegramUserName}", ct);
            await telegramBotClient.SendMessage(update.Message.Chat, $" Registered Date: {newUser.RegisteredAt}", ct);
            displayName = newUser.TelegramUserName;
        }

        private async Task MyNameIsAsync(ITelegramBotClient telegramBotClient, Update update, string text, CancellationToken ct)
        {
            var command = text.ToLower().Split(' ')[0];

            if (command == "/start")
            {
                ToDoUser? user = await _userService.GetUserAsync(_userId);

                if (user == null)
                {
                    await telegramBotClient.SendMessage(update.Message.Chat, " Вы еще не зарегистрированы. Хотите принять участие в проекте \"Кулинарный Бот\"?", ct);
                    await telegramBotClient.SendMessage(update.Message.Chat, " Для регистрации введите \"Y\" ", ct);
                    _state = HandlerState.AwaitingRegistration;
                }
                else
                {
                    displayName = user.TelegramUserName;
                    await telegramBotClient.SendMessage(update.Message.Chat, $" {user.TelegramUserName} Добро пожаловать", ct);
                    _state = HandlerState.Ready;
                    await telegramBotClient.SendMessage(update.Message.Chat, $"{displayName}, Введите команду: ", ct);
                }
            }
            else if (command == "/help")
            {
                await HelpAsync(telegramBotClient, update, ct);
            }
            else if (command == "/info")
            {
                await InfoAsync(telegramBotClient, update, ct);
            }
            else
            {
                await telegramBotClient.SendMessage(update.Message.Chat, " Бот не знает такой команды либо эта команда недоступна\n Для начала работы введите \"/start\"", ct);
            }
        }

        private async Task HelpAsync(ITelegramBotClient telegramBotClient, Update update, CancellationToken ct)
        {
            var user = await _userService.GetUserAsync(_userId);

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
                str.AppendLine(" \"/addtask Задача\" - позволяет добавить Задачу, между командой и Задачей обязательно должен быть пробел");
                str.AppendLine(" \"/showtasks\" - отображает все \"Активные\" задачи");
                str.AppendLine(" \"/showalltasks\" - отображает все задачи");
                str.AppendLine(" \"/removetask Идентификатор\" - позволяет удалить доступную задачу по ее Идентификатору, между командой и Идентификатором обязательно должен быть пробел");
                str.AppendLine(" \"/completetask Идентификатор\" - позволяет изменить состояние задачи с \"Активная\" на \"Завершенная\", между командой и Идентификатором обязательно должен быть пробел");
                str.AppendLine(" \"/report\" - отображает статистику по задачам текущего пользователя на данный момент времени");
                str.AppendLine(" \"/find Имя\" - отображает все задачи зарегистрированного пользователя с именем \"Имя\", между командой и Именем обязательно должен быть пробел");
            }
            str.AppendLine(" \"/exit\" - завершает работу Бота\n");
            await telegramBotClient.SendMessage(update.Message.Chat, str.ToString(), ct);
        }

        private async Task InfoAsync(ITelegramBotClient telegramBotClient, Update update, CancellationToken ct)
        {
            string createDate = " Created 21.05.2026    ";

            Assembly assembly = Assembly.GetExecutingAssembly();
            AssemblyName assemblyName = assembly.GetName();
            Version version = assemblyName.Version;

            await telegramBotClient.SendMessage(update.Message.Chat, $"{createDate} The Version used {version}", ct);
        }

        private async Task AddTaskAsync(string inputStr, ITelegramBotClient telegramBotClient, Update update, CancellationToken ct)
        {
            try
            {
                var parts = inputStr.Split(' ', 2);
                string newTask = parts.Length > 1 ? parts[1] : string.Empty;

                if (!string.IsNullOrWhiteSpace(newTask))
                {
                    var user = await _userService.GetUserAsync(_userId);
                    ToDoItem newToDoItem = await _todoService.AddAsync(user!, newTask);

                    var str = new StringBuilder();
                    str.AppendLine($" Задача добавлена:\n");
                    str.AppendLine($" Id: {newToDoItem.Id}\n");
                    str.AppendLine($" User:\tUserId: {newToDoItem.User.UserId}\n");
                    str.AppendLine($" \t\tTelegramUserId: {newToDoItem.User.TelegramUserId}\n");
                    str.AppendLine($" \t\tTelegramUserName: {newToDoItem.User.TelegramUserName}\n");
                    str.AppendLine($" \t\tRegistered Date: {newToDoItem.User.RegisteredAt}\n");
                    str.AppendLine($" Name: {newToDoItem.Name}\n");
                    str.AppendLine($" CreatedAt: {newToDoItem.CreatedAt}\n");
                    str.AppendLine($" State: {newToDoItem.State}\n");
                    str.AppendLine($" StateChangedAt: {newToDoItem.StateChangedAt}\n");
                    await telegramBotClient.SendMessage(update.Message.Chat, str.ToString(), ct);
                }
                else
                {
                    await telegramBotClient.SendMessage(update.Message.Chat, " Аргумент для команды \"/addtask\" отсутствует", ct);
                }
            }
            catch (TaskCountLimitException e)
            {
                await telegramBotClient.SendMessage(update.Message.Chat, $"Превышено максимальное количество задач равное {e.TaskCountLimit}", ct);
            }
            catch (TaskLengthLimitException e)
            {
                await telegramBotClient.SendMessage(update.Message.Chat, $"Длина задачи ‘{e.TaskLength}’ превышает максимально допустимое значение {e.TaskLengthLimit}", ct);
            }
            catch (DuplicateTaskException e)
            {
                await telegramBotClient.SendMessage(update.Message.Chat, $"Задача ‘{e.Task}’ уже существует", ct);
            }
        }

        private async Task ShowTasksAsync(ITelegramBotClient telegramBotClient, Update update, CancellationToken ct)
        {
            var user = await _userService.GetUserAsync(_userId);
            var listTasks = (await _todoService.GetAllByUserIdAsync(user!.UserId));

            if (listTasks.Count() > 0)
            {
                for (int i = 0; i < listTasks.Count(); i++)
                {
                    if (listTasks[i].State == ToDoItem.ToDoItemState.Active)
                        await telegramBotClient.SendMessage(update.Message.Chat, $"{i + 1}. {listTasks[i].Name} - {listTasks[i].CreatedAt} - {listTasks[i].Id}", ct);
                }
            }
            else
            {
                await telegramBotClient.SendMessage(update.Message.Chat, " Список задач пуст", ct);
            }
        }

        private async Task ShowAllTasksAsync(ITelegramBotClient telegramBotClient, Update update, CancellationToken ct)
        {
            var user = await _userService.GetUserAsync(_userId);
            var listAllTasks = await _todoService.GetAllByUserIdAsync(user!.UserId);

            if (listAllTasks.Count() > 0)
            {

                for (int i = 0; i < listAllTasks.Count(); i++)
                {
                    if (listAllTasks[i].State == ToDoItem.ToDoItemState.Active)
                        await telegramBotClient.SendMessage(update.Message.Chat, $"{i + 1}. (Active) {listAllTasks[i].Name} - {listAllTasks[i].CreatedAt} - {listAllTasks[i].Id}", ct);
                    else if (listAllTasks[i].State == ToDoItem.ToDoItemState.Completed)
                        await telegramBotClient.SendMessage(update.Message.Chat, $"{i + 1}. (Complete) {listAllTasks[i].Name} - {listAllTasks[i].CreatedAt} - {listAllTasks[i].Id}", ct);
                }
            }
            else
            {
                await telegramBotClient.SendMessage(update.Message.Chat, " Список задач пуст", ct);
            }
        }

        private async Task CompleteTaskAsync(string inputStr, ITelegramBotClient telegramBotClient, Update update, CancellationToken ct)
        {
            var user = await _userService.GetUserAsync(_userId);
            var listTasks = await _todoService.GetActiveByUserIdAsync(user!.UserId);

            if (listTasks.Count() > 0)
            {
                var parts = inputStr.Split(' ', 2);
                string selectedId = parts.Length > 1 ? parts[1] : string.Empty;

                Guid num = default(Guid);

                if (Guid.TryParse(selectedId, out num))
                {
                    if (num == default(Guid))
                    {
                        await telegramBotClient.SendMessage(update.Message.Chat, " Необходимо ввести именно Идентификатор задачи, попробуйте еще раз: ", ct);
                    }
                    else
                    {
                        var target = listTasks.Where(w => w.Id == num).FirstOrDefault();
                        if (target == null)
                        {
                            await telegramBotClient.SendMessage(update.Message.Chat, " Задачи с таким номером нет, попробуйте еще раз: ", ct);
                        }
                        else
                        {
                            await _todoService.MarkCompletedAsync(num);
                            await telegramBotClient.SendMessage(update.Message.Chat, $" Команда с Именем \"{target.Name}\" выполнена", ct);
                        }
                    }
                }
                else
                {
                    await telegramBotClient.SendMessage(update.Message.Chat, " Аргумент для команды \"/completetask\" отсутствует", ct);
                }
            }
            else
            {
                await telegramBotClient.SendMessage(update.Message.Chat, " Список задач пуст", ct);
            }
        }

        private async Task RemoveTaskAsync(string inputStr, ITelegramBotClient telegramBotClient, Update update, CancellationToken ct)
        {
            var user = await _userService.GetUserAsync(_userId);
            var listAllTasks = await _todoService.GetAllByUserIdAsync(user!.UserId);

            if (listAllTasks.Count() > 0)
            {
                var parts = inputStr.Split(' ', 2);
                string selectedId = parts.Length > 1 ? parts[1] : string.Empty;

                Guid num = default(Guid);

                if (Guid.TryParse(selectedId, out num))
                {
                    if (num == default(Guid))
                    {
                        await telegramBotClient.SendMessage(update.Message.Chat, " Необходимо ввести именно Идентификатор задачи, попробуйте еще раз: ", ct);
                    }
                    else
                    {
                        var target = listAllTasks.FirstOrDefault(w => w.Id == num);
                        if (target == null)
                        {
                            await telegramBotClient.SendMessage(update.Message.Chat, " Задачи с таким номером нет, попробуйте еще раз: ", ct);
                        }
                        else
                        {
                            await telegramBotClient.SendMessage(update.Message.Chat, $@" Задача ""{target.Name}"" удалена", ct);
                            await _todoService.DeleteAsync(num);
                        }
                    }
                }
                await ShowAllTasksAsync(telegramBotClient, update, ct);
            }
            else
            {
                await telegramBotClient.SendMessage(update.Message.Chat, " Список задач пуст", ct);
            }
        }

        private async Task ReportAsync(ITelegramBotClient telegramBotClient, Update update, CancellationToken ct)
        {
            var user = await _userService.GetUserAsync(_userId);
            var (total, completed, active, generatedAt) = await _toDoReportService.GetUserStatsAsync(user!.UserId);
            string _generatedAt = generatedAt.ToShortDateString();
            int _total = total;
            int _active = active;
            int _completed = completed;

            await telegramBotClient.SendMessage(update.Message.Chat, $" Статистика по задачам на {_generatedAt}", ct);
            await telegramBotClient.SendMessage(update.Message.Chat, $" Всего: {_total}", ct);
            await telegramBotClient.SendMessage(update.Message.Chat, $" Завершенных: {_completed}", ct);
            await telegramBotClient.SendMessage(update.Message.Chat, $" Активных: {_active}", ct);
        }

        private async Task FindAsync(string inputStr, ITelegramBotClient telegramBotClient, Update update, CancellationToken ct)
        {
            var parts = inputStr.Split(' ', 2);
            string namePrefix = parts.Length > 1 ? parts[1] : string.Empty;

            if (!string.IsNullOrWhiteSpace(namePrefix))
            {
                var user = await _userService.GetUserAsync(_userId);
                var listTasks = await _todoService.FindAsync(user!, namePrefix);

                if (listTasks.Count() > 0)
                {
                    for (int i = 0; i < listTasks.Count(); i++)
                    {
                        await telegramBotClient.SendMessage(update.Message.Chat, $"{i + 1}. {listTasks[i].Name} - {listTasks[i].CreatedAt} - {listTasks[i].Id}", ct);
                    }
                }
                else
                {
                    await telegramBotClient.SendMessage(update.Message.Chat, " Список задач пуст", ct);
                }
            }
            else
            {
                await telegramBotClient.SendMessage(update.Message.Chat, " Аргумент для команды \"/find\" отсутствует", ct);
            }
        }
    }
}
