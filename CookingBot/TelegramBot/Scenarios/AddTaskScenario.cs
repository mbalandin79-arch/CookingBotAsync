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
                var chat = update.Message?.Chat;
                if (chat == null)
                    return ScenarioResult.Completed;

                switch(context.CurrentStep)
                {
                    case null:
                        {
                            var user = await _userService.GetUserAsync(context.UserId, ct);
                            if (user == null)
                            {
                                await telegramBotClient.SendMessage(chat, "Вы не зарегистрированы. Выберите \"Старт\"", cancellationToken: ct);
                                return ScenarioResult.Completed;
                            }
                            context.Data["user"] = user;
                            await telegramBotClient.SendMessage(chat, "Введите название задачи:", cancellationToken: ct);
                            context.CurrentStep = "Name";
                            return ScenarioResult.Transition;
                        }
                    case "Name":
                        {
                            var name = update.Message?.Text;
                            if (string.IsNullOrWhiteSpace(name))
                            {
                                await telegramBotClient.SendMessage(chat, "Название не может быть пустым. Введите название задачи:", cancellationToken: ct);
                                return ScenarioResult.Transition;
                            }
                            context.Data["name"] = name;
                            await telegramBotClient.SendMessage(chat, "Введите дедлайн (формат dd.MM.yyyy):", cancellationToken: ct);
                            context.CurrentStep = "Deadline";
                            return ScenarioResult.Transition;
                        }
                    case "Deadline":
                        {
                            var deadlineText = update.Message?.Text;
                            if (!DateTime.TryParseExact(deadlineText, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var deadline))
                            {
                                await telegramBotClient.SendMessage(chat, "Неверный формат даты. Введите дедлайн в формате dd.MM.yyyy:", cancellationToken: ct);
                                return ScenarioResult.Transition;
                            }

                            var toDoUser = (ToDoUser)context.Data["user"];
                            var taskName = (string)context.Data["name"];

                            try
                            {
                                var item = await _todoService.AddAsync(toDoUser, taskName, deadline, ct);
                                var str = new StringBuilder();
                                str.AppendLine("Задача добавлена:");
                                str.AppendLine($" Id: {item.Id}");
                                str.AppendLine($" Name: {item.Name}");
                                str.AppendLine($" CreatedAt: {item.CreatedAt}");
                                str.AppendLine($" Deadline: {item.Deadline:dd.MM.yyyy}");
                                str.AppendLine($" State: {item.State}");
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
    }
}
