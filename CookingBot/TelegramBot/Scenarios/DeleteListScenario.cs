using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CookingBot.Core.Entities;
using CookingBot.Core.Services;
using CookingBot.Helpers;
using CookingBot.TelegramBot.Dto;
using Telegram.Bot;
using Telegram.Bot.Types;
using static CookingBot.TelegramBot.Scenarios.ScenarioContext;

namespace CookingBot.TelegramBot.Scenarios
{
    public class DeleteListScenario : IScenario
    {
        private readonly IUserService _userService;
        private readonly IToDoListService _todoListService;
        private readonly IToDoService _todoService;

        public DeleteListScenario(IUserService userService, IToDoListService todoListService, 
            IToDoService toDoService)
        {
            _userService = userService;
            _todoListService = todoListService;
            _todoService = toDoService;
        }

        public bool CanHandle(ScenarioContext.ScenarioType scenario)
        {
            if (scenario == ScenarioType.DeleteList)
                return true;

            return false;
        }

        public async Task<ScenarioContext.ScenarioResult> HandleMessageAsync(ITelegramBotClient telegramBotClient, 
            ScenarioContext context, Update update, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var chat = update.Message?.Chat ?? update.CallbackQuery?.Message?.Chat;
            if (chat == null)
                return ScenarioResult.Completed;

            switch (context.CurrentStep)
            {
                case null:
                    {
                        var prompt = string.Empty;
                        var user = await _userService.GetUserAsync(context.UserId, ct);
                        if (user == null)
                        {
                            prompt = "Вы не зарегистрированы. Выберите \"Старт\"";
                            await telegramBotClient.SendMessage(chat, prompt, cancellationToken: ct);
                            return ScenarioResult.Completed;
                        }
                        context.Data["user"] = user;

                        List<ToDoList> lists = (List<ToDoList>)await _todoListService.GetUserListsAsync(user.UserId, ct);
                        if (lists.Count == 0)
                        {
                            prompt = "У вас нет списков для удаления";
                            await telegramBotClient.SendMessage(chat, prompt, cancellationToken: ct);
                            return ScenarioResult.Completed;

                        }

                        prompt = "Выберите список для удаления:";
                        await telegramBotClient.SendMessage(chat, prompt, 
                            replyMarkup: Keyboards.BuildKeyboardDeleteListForUser(lists), cancellationToken: ct);

                        context.CurrentStep = "Approve";
                        return ScenarioResult.Transition;
                    }
                case "Approve":
                    {
                        var prompt = string.Empty;
                        var data = update.CallbackQuery?.Data;
                        if (string.IsNullOrEmpty(data))
                            return ScenarioResult.Completed;

                        var dto = ToDoListCallbackDto.FromString(data);
                        if (dto.ToDoListId == null)
                        {
                            prompt = "Не удалось определить список.";
                            await telegramBotClient.SendMessage(chat, prompt, cancellationToken: ct);
                            return ScenarioResult.Completed;
                        }

                        var list = await _todoListService.GetAsync(dto.ToDoListId.Value, ct);
                        if (list == null)
                        {
                            await telegramBotClient.SendMessage(chat, "Список не найден.", cancellationToken: ct);
                            return ScenarioResult.Completed;
                        }

                        context.Data["list"] = list;

                        prompt = $"Подтвердите удаление списка '{list.Name}' и всех его рецептов:";
                        await telegramBotClient.SendMessage(chat, prompt, 
                            replyMarkup: Keyboards.BuildKeyboardYesNo(), cancellationToken: ct);
                        context.CurrentStep = "Delete";
                        return ScenarioResult.Transition;
                    }
                case "Delete":
                    {
                        var answer = update.CallbackQuery?.Data;

                        if (answer == "no")
                        {
                            await telegramBotClient.SendMessage(chat, "Удаление отменено.", cancellationToken: ct);
                            return ScenarioResult.Completed;
                        }

                        if (answer == "yes")
                        {
                            try
                            {
                                var list = (ToDoList)context.Data["list"];
                                var user = (ToDoUser)context.Data["user"];

                                var items = await _todoService.GetByUserIdAndList(user.UserId, list.Id, ct);
                                foreach (var item in items)
                                {
                                    await _todoService.DeleteAsync(item.Id, ct);
                                }

                                await _todoListService.DeleteAsync(list.Id, ct);
                                var prompt = $"Список '{list.Name}' удалён.";
                                await telegramBotClient.SendMessage(chat, prompt, cancellationToken: ct);
                            }
                            catch (Exception e) 
                            {
                                FileLogger.LogError(e, "DeleteListScenario");
                                var prompt = "Не удалось удалить список. Подробности записаны в лог.";
                                await telegramBotClient.SendMessage(chat, prompt, cancellationToken: ct);

                            }
                        }

                        return ScenarioResult.Completed;
                    }
                default:
                    return ScenarioResult.Completed;
            }
        }
    }
}
