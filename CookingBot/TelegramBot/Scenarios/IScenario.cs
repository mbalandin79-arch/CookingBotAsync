using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CookingBot.TelegramBot.Scenarios.ScenarioContext;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace CookingBot.TelegramBot.Scenarios
{
    /// <summary>
    /// Сценарий многошагового диалога с пользователем (добавление рецепта,
    /// удаление и т.п.). UpdateHandler по ScenarioContext.CurrentScenario
    /// находит нужный сценарий и передаёт ему сообщения пользователя.
    /// Реализации: AddTaskScenario, AddListScenario, DeleteTaskScenario, DeleteListScenario.
    /// </summary>
    public interface IScenario
    {
        /// <summary>
        /// Проверяет, может ли этот сценарий обрабатывать указанный тип.
        /// Используется для определения подходящего обработчика при запуске сценария.
        /// </summary>
        /// <param name="scenario">Тип сценария.</param>
        /// <returns>true, если сценарий отвечает за указанный тип.</returns>
        bool CanHandle(ScenarioType scenario);

        /// <summary>
        /// Обрабатывает очередное сообщение пользователя внутри активного сценария
        /// (основная бизнес-логика шагов диалога).
        /// </summary>
        /// <param name="telegramBotClient">Клиент Telegram для отправки ответов.</param>
        /// <param name="context">Контекст сценария (данные между шагами).</param>
        /// <param name="update">Входящее обновление от пользователя.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Transition — сообщение обработано, сценарий продолжается; Completed — сценарий завершён.</returns>
        Task<ScenarioResult> HandleMessageAsync(ITelegramBotClient telegramBotClient, ScenarioContext context, Update update, CancellationToken ct);
    }
}
