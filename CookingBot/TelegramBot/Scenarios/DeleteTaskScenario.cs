using CookingBot.Core.Entities;
using CookingBot.Core.Services;
using CookingBot.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using static CookingBot.TelegramBot.Scenarios.ScenarioContext;

namespace CookingBot.TelegramBot.Scenarios
{
    public class DeleteTaskScenario : IScenario
    {
        private readonly IToDoService _todoService;

        public DeleteTaskScenario(IToDoService todoService)
        {
            _todoService = todoService;
        }

        public bool CanHandle(ScenarioContext.ScenarioType scenario)
        {
            if (scenario == ScenarioType.DeleteTask)
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
                        var taskId = (Guid)context.Data["taskId"];
                        var task = await _todoService.GetTaskAsync(taskId, ct);
                        if (task == null)
                        {
                            await telegramBotClient.SendMessage(chat, "Рецепт не найден.", cancellationToken: ct);
                            return ScenarioResult.Completed;
                        }

                        context.Data["task"] = task;

                        var prompt = $"Подтвердите удаление рецепта '{task.Name}':";
                        await telegramBotClient.SendMessage(chat, prompt, replyMarkup: Keyboards.BuildKeyboardYesNo(), 
                            cancellationToken: ct);
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
                                var task = (ToDoItem)context.Data["task"];
                                await _todoService.DeleteAsync(task.Id, ct);
                                var prompt = $"Рецепт '{task.Name}' удалён.";
                                await telegramBotClient.SendMessage(chat, prompt, cancellationToken: ct);
                            }
                            catch (Exception e)
                            {
                                FileLogger.LogError(e, "DeleteTaskScenario");
                                var prompt = "Не удалось удалить рецепт. Подробности записаны в лог.";
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
