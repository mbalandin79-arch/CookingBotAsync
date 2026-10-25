using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CookingBot.Core.Entities;
using LinqToDB.Mapping;

namespace CookingBot.Core.DataAccess.Models
{
    /// <summary>
    /// Модель таблицы Notification (нотификации) для linq2db.
    /// Соответствует Core-сущности Notification; преобразование выполняет ModelMapper.
    /// </summary>
    [Table("Notification")]
    public class NotificationModel
    {
        /// <summary>Идентификатор нотификации (первичный ключ).</summary>
        [PrimaryKey]
        [Column("Id")]
        public Guid Id { get; set; }

        /// <summary>Идентификатор получателя (FK -> "ToDoUser"."UserId").</summary>
        [Column("UserId")]
        public Guid UserId { get; set; }

        /// <summary>Получатель (заполняется linq2db при LoadWith).</summary>
        [Association(ThisKey = nameof(UserId), OtherKey = nameof(ToDoUserModel.UserId))]
        public ToDoUserModel User { get; set; } = null!;

        /// <summary>Тип нотификации (например: DeadLine_{Id рецепта}, Today_{дата}).</summary>
        [Column("Type")]
        public string Type { get; set; } = null!;

        /// <summary>Текст, который будет отправлен пользователю.</summary>
        [Column("Text")]
        public string Text { get; set; } = null!;

        /// <summary>Запланированная дата отправки (в UTC).</summary>
        [Column("ScheduledAt")]
        public DateTime ScheduledAt { get; set; }

        /// <summary>Флаг отправки: false — ждёт отправки, true — отправлена.</summary>
        [Column("IsNotified")]
        public bool IsNotified { get; set; }

        /// <summary>Фактическая дата отправки; null, пока не отправлена.</summary>
        [Column("NotifiedAt")]
        public DateTime? NotifiedAt { get; set; }
    }
}
