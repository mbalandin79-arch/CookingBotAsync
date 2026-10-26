using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CookingBot.Core.Entities;
using CookingBot.Core.Services;
using CookingBot.Helpers;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace CookingBot.BackgroundTasks
{
    /// <summary>
    /// Фоновая задача: раз в минуту отправляет запланированные
    /// нотификации — выбирает неотправленные, время которых наступило,
    /// рассылает их пользователям через Telegram и помечает отправленными.
    /// </summary>
    public class NotificationBackgroundTask : BackgroundTask
    {
        private readonly INotificationService _notificationService;
        private readonly ITelegramBotClient _bot;

        /// <summary>Создаёт задачу отправки нотификаций.</summary>
        /// <param name="notificationService">Сервис нотификаций (выборка неотправленных, отметка об отправке).</param>
        /// <param name="bot">Клиент Telegram для отправки сообщений пользователям.</param>
        public NotificationBackgroundTask(INotificationService notificationService, ITelegramBotClient bot)
            : base(TimeSpan.FromMinutes(1), nameof(NotificationBackgroundTask))
        {
            _notificationService = notificationService;
            _bot = bot;
        }

        /// <inheritdoc/>
        protected override async Task Execute(CancellationToken ct)
        {
            // неотправленные нотификации, запланированные на уже наступившее время
            var notifications = await _notificationService.GetScheduledNotification(DateTime.UtcNow, ct);

            foreach (var notification in notifications)
            {
                try
                {
                    // отправляем пользователю (приватный чат: ChatId = TelegramUserId)
                    await _bot.SendMessage(new ChatId(notification.User.TelegramUserId),
                        notification.Text, cancellationToken: ct);

                    // помечаем отправленной только после успешной отправки:
                    // при ошибке (например, пользователь заблокировал бота)
                    // попытка повторится на следующем такте задачи
                    await _notificationService.MarkNotified(notification.Id, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    FileLogger.LogError(ex, $"NotificationBackgroundTask: не удалось отправить нотификацию {notification.Id}");
                }
            }
        }
    }
}
