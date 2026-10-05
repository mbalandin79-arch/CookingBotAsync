using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CookingBot.Core.Entities
{
    public class ToDoUser
    {
        public enum ToDoUserState
        {
            Guest,
            Member,
            Advanced,
            Moderator,
            Admin
        }
        public Guid UserId { get; set; }
        public string TelegramUserName { get; set; }
        public DateTime RegisteredAt { get; set; }
        public long TelegramUserId { get; set; }
        public ToDoUserState State { get; set; }

        public static string GetStateName(ToDoUserState state)
        {
            return state switch
            {
                ToDoUserState.Guest => "Гость",
                ToDoUserState.Member => "Участник",
                ToDoUserState.Advanced => "Активный участник",
                ToDoUserState.Moderator => "Модератор",
                ToDoUserState.Admin => "Администратор",
                _ => state.ToString()
            };
        }
    }
}
