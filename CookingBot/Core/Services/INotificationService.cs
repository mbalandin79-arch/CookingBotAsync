using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CookingBot.Core.Entities;

namespace CookingBot.Core.Services
{
    /// <summary>
    /// Сервис нотификаций: планирование уведомлений, выборка
    /// неотправленных и отметка об отправке. Реализация — NotificationService
    /// в Infrastructure (работает с БД напрямую, без репозиториев).
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Создаёт нотификацию. Если запись с userId и type уже есть,
        /// возвращает false и не добавляет запись, иначе — true.
        /// </summary>
        /// <param name="userId">Идентификатор пользователя-получателя.</param>
        /// <param name="type">Тип нотификации (защита от дублей).</param>
        /// <param name="text">Текст, который будет отправлен.</param>
        /// <param name="scheduledAt">Запланированная дата отправки.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>true — запись добавлена; false — запись с такой парой userId+type уже есть.</returns>
        Task<bool> ScheduleNotification(Guid userId, string type, string text, DateTime scheduledAt, CancellationToken ct);

        /// <summary>
        /// Возвращает нотификации, у которых IsNotified = false
        /// и ScheduledAt &lt;= scheduledBefore (с заполненным User).
        /// </summary>
        /// <param name="scheduledBefore">Верхняя граница запланированной даты.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Список неотправленных нотификаций, время которых наступило.</returns>
        Task<IReadOnlyList<Notification>> GetScheduledNotification(DateTime scheduledBefore, CancellationToken ct);

        /// <summary>Помечает нотификацию отправленной (IsNotified = true, NotifiedAt = текущее время).</summary>
        /// <param name="notificationId">Идентификатор нотификации.</param>
        /// <param name="ct">Токен отмены операции.</param>
        Task MarkNotified(Guid notificationId, CancellationToken ct);
    }
}
