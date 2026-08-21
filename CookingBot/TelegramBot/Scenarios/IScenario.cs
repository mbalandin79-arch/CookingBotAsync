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
    public interface IScenario
    {
        // Проверяет, может ли текущий сценарий обрабатывать указанный тип сценария.
        // Используется для определения подходящего обработчика в системе сценариев
        bool CanHandle(ScenarioType scenario);

        // Обрабатывает входящее сообщение от пользователя в рамках текущего сценария.
        // Включает основную бизнес-логику
        Task<ScenarioResult> HandleMessageAsync(ITelegramBotClient telegramBotClient, ScenarioContext context, Update update, CancellationToken ct);
    }
}
