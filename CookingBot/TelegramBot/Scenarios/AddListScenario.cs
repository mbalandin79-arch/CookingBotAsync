using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CookingBot.Core.Entities;
using CookingBot.Core.Exceptions;
using CookingBot.Core.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using static CookingBot.TelegramBot.Scenarios.ScenarioContext;

namespace CookingBot.TelegramBot.Scenarios
{
    public class AddListScenario : IScenario
    {
        private readonly IUserService _userService;
        private readonly IToDoListService _todoListService;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public AddListScenario(IUserService userService, IToDoListService todoListService)
        {
            _userService = userService;
            _todoListService = todoListService;
        }

        public bool CanHandle(ScenarioContext.ScenarioType scenario)
        {
            if (scenario == ScenarioType.AddList)
                return true;

            return false;
        }

        public async Task<ScenarioContext.ScenarioResult> HandleMessageAsync(ITelegramBotClient telegramBotClient, ScenarioContext context, Update update, CancellationToken ct)
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
                    case null:
                        {
                            var user = await _userService.GetUserAsync(context.UserId, ct);
                            if (user == null)
                            {
                                await telegramBotClient.SendMessage(chat, "Вы не зарегистрированы. Выберите \"Старт\"", cancellationToken: ct);
                                return ScenarioResult.Completed;
                            }
                            context.Data["user"] = user;
                            await telegramBotClient.SendMessage(chat, "Введите название списка (не более 10 символов):", cancellationToken: ct);
                            context.CurrentStep = "Name";
                            return ScenarioResult.Transition;
                        }
                    case "Name":
                        {
                            var name = update.Message?.Text;
                            if (string.IsNullOrWhiteSpace(name))
                            {
                                await telegramBotClient.SendMessage(chat, "Название не может быть пустым. Введите название списка:", cancellationToken: ct);
                                return ScenarioResult.Transition;
                            }

                            name = name.Trim();

                            var toDoUser = (ToDoUser)context.Data["user"];

                            try
                            {
                                var list = await _todoListService.AddAsync(toDoUser, name, ct);
                                await telegramBotClient.SendMessage(chat, $"Список '{list.Name}' добавлен.", cancellationToken: ct);
                                return ScenarioResult.Completed;
                            }                            
                            catch (TaskLengthLimitException e)
                            {
                                await telegramBotClient.SendMessage(chat, $"Длина названия '{e.TaskLength}' превышает максимум {e.TaskLengthLimit} символов", cancellationToken: ct);
                                return ScenarioResult.Completed;
                            }
                            catch (DuplicateTaskException e)
                            {
                                await telegramBotClient.SendMessage(chat, $"Список '{e.Task}' уже существует", cancellationToken: ct);
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
