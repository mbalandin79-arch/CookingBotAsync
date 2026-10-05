using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LinqToDB.Mapping;
using static CookingBot.Core.Entities.ToDoUser;

namespace CookingBot.Core.DataAccess.Models
{
    [Table("ToDoUser")]
    public class ToDoUserModel
    {
        [PrimaryKey]
        [Column("UserId")]
        public Guid UserId { get; set; }

        [Column("TelegramUserName")]
        public string TelegramUserName { get; set; } = null!;

        [Column("RegisteredAt")]
        public DateTime RegisteredAt { get; set; }

        [Column("TelegramUserId")]
        public long TelegramUserId { get; set; }

        [Column("State")]
        public ToDoUserState State { get; set; }
    }
}
