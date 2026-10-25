using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LinqToDB.Mapping;
using static CookingBot.Core.Entities.ToDoUser;

namespace CookingBot.Core.DataAccess.Models
{
    /// <summary>
    /// Модель таблицы ToDoUser для linq2db (отображение пользователя в БД).
    /// Преобразование в Core-сущность ToDoUser и обратно выполняет ModelMapper.
    /// </summary>
    [Table("ToDoUser")]
    public class ToDoUserModel
    {
        /// <summary>Уникальный идентификатор пользователя (первичный ключ).</summary>
        [PrimaryKey]
        [Column("UserId")]
        public Guid UserId { get; set; }

        /// <summary>Имя пользователя в Telegram.</summary>
        [Column("TelegramUserName")]
        public string TelegramUserName { get; set; } = null!;

        /// <summary>Дата и время регистрации пользователя.</summary>
        [Column("RegisteredAt")]
        public DateTime RegisteredAt { get; set; }

        /// <summary>Идентификатор пользователя в Telegram.</summary>
        [Column("TelegramUserId")]
        public long TelegramUserId { get; set; }

        /// <summary>Уровень доступа пользователя (роль).</summary>
        [Column("State")]
        public ToDoUserState State { get; set; }
    }
}
