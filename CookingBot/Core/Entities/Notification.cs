using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CookingBot.Core.Entities
{
    /// <summary>
    /// Запланированное уведомление (нотификация) для пользователя бота.
    /// Создаётся фоновыми задачами (по просроченным дедлайнам и задачам на день),
    /// а отправляется пользователю в Telegram задачей NotificationBackgroundTask.
    /// </summary>
    public class Notification
    {
        /// <summary>Идентификатор нотификации (первичный ключ).</summary>
        public Guid Id { get; set; }

        /// <summary>Получатель нотификации.</summary>
        public ToDoUser User { get; set; } = null!;

        /// <summary>
        /// Тип нотификации, защита от дублей (одна запись на пару пользователь+тип).
        /// Например: DeadLine_{Id рецепта}, Today_{дата}.
        /// </summary>
        public string Type { get; set; } = null!;

        /// <summary>Текст, который будет отправлен пользователю.</summary>
        public string Text { get; set; } = null!;

        /// <summary>Запланированная дата отправки (в UTC).</summary>
        public DateTime ScheduledAt { get; set; }

        /// <summary>Флаг отправки: false — ждёт отправки, true — отправлена.</summary>
        public bool IsNotified { get; set; }

        /// <summary>Фактическая дата отправки; null, пока не отправлена.</summary>
        public DateTime? NotifiedAt { get; set; }
    }
}
