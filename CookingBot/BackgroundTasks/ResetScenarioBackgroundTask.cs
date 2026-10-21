using CookingBot.TelegramBot.Scenarios;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace CookingBot.BackgroundTasks
{
    /// <summary>
    /// Фоновая задача: периодически проверяет контексты сценариев и отменяет
    /// "зависшие" — те, на которые пользователь не отвечает дольше resetScenarioTimeout.
    /// </summary>
    public class ResetScenarioBackgroundTask : BackgroundTask
    {
        private readonly TimeSpan _resetScenarioTimeout;
        private readonly IScenarioContextRepository _scenarioRepository;
        private readonly ITelegramBotClient _bot;

        /// <summary>
        /// Создаёт задачу сброса зависших сценариев.
        /// </summary>
        /// <param name="resetScenarioTimeout">Через какое время после создания отменять сценарий.</param>
        /// <param name="scenarioRepository">Репозиторий контекстов сценариев.</param>
        /// <param name="bot">Клиент Telegram для отправки сообщений пользователю.</param>
        public ResetScenarioBackgroundTask(TimeSpan resetScenarioTimeout, IScenarioContextRepository scenarioRepository, ITelegramBotClient bot)
            : base(TimeSpan.FromHours(1), nameof(ResetScenarioBackgroundTask))
        {
            _resetScenarioTimeout = resetScenarioTimeout;
            _scenarioRepository = scenarioRepository;
            _bot = bot;
        }

        protected override async Task Execute(CancellationToken ct)
        {
            var contexts = await _scenarioRepository.GetContexts(ct);

            foreach (var context in contexts)
            {
                // Контекст "моложе" таймаута - еще актуален, пропускаем
                if (DateTime.UtcNow - context.CreatedAt < _resetScenarioTimeout)
                {
                    continue;
                }

                await _scenarioRepository.ResetContext(context.UserId, ct);

                await _bot.SendMessage(new ChatId(context.UserId),
                    $"Сценарий отменен, так как не поступил ответ в течение {_resetScenarioTimeout}",
                    replyMarkup: new ReplyKeyboardMarkup(new[]
                    {
                        new KeyboardButton[] { "/addtask", "/show", "/report" }
                    }),
                    cancellationToken: ct);
            }
        }
    }
}
