using CookingBot.TelegramBot.Scenarios;
using CookingBot.TelegramBot;
using CookingBot.Core.Services;
using System;
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
        private readonly IUserService _userService;
        private readonly ITelegramBotClient _bot;

        /// <summary>Создаёт задачу сброса зависших сценариев.</summary>
        /// <param name="runInterval">Интервал запуска задачи (настраивается в appsettings.json).</param>
        /// <param name="resetScenarioTimeout">Через какое время после создания отменять сценарий.</param>
        /// <param name="scenarioRepository">Репозиторий контекстов сценариев.</param>
        /// <param name="userService">Сервис пользователей: по роли подбирается клавиатура сообщения.</param>
        /// <param name="bot">Клиент Telegram для отправки сообщений пользователю.</param>
        public ResetScenarioBackgroundTask(TimeSpan runInterval, TimeSpan resetScenarioTimeout, 
            IScenarioContextRepository scenarioRepository, IUserService userService, ITelegramBotClient bot)
            : base(runInterval, nameof(ResetScenarioBackgroundTask))
        {
            _resetScenarioTimeout = resetScenarioTimeout;
            _scenarioRepository = scenarioRepository;
            _userService = userService;
            _bot = bot;
        }

        /// <inheritdoc/>
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

                // Состав кнопок клавиатуры зависит от роли пользователя
                var user = await _userService.GetUserAsync(context.UserId, ct);

                // Как при ручной отмене (/cancel): убираем кнопку "Отмена"
                // и показываем главное меню по роли пользователя
                await _bot.SendMessage(new ChatId(context.UserId),
                    $"Сценарий отменен, так как не поступил ответ в течение {_resetScenarioTimeout}",
                    replyMarkup: new ReplyKeyboardRemove(), cancellationToken: ct);

                await _bot.SendMessage(new ChatId(context.UserId), "Выберите раздел:",
                    replyMarkup: Keyboards.BuildMainMenuKeyboard(user), cancellationToken: ct);
            }
        }
    }
}
