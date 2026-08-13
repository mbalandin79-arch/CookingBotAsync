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
        public Guid UserId { get; }
        public string TelegramUserName { get; set; }
        public DateTime RegisteredAt { get; }
        public long TelegramUserId { get; }
        public ToDoUserState State { get; set; }

        public ToDoUser(long telegramUserId, string telegramUserName)
        {
            TelegramUserId = telegramUserId;
            TelegramUserName = telegramUserName;
            RegisteredAt = DateTime.UtcNow;
            UserId = Guid.NewGuid();
            State = ToDoUserState.Guest;
        }

        [JsonConstructor]
        public ToDoUser(Guid userId, long telegramUserId, string telegramUserName, DateTime registeredAt, ToDoUserState state) 
        { 
            UserId = userId;
            TelegramUserId = telegramUserId;
            TelegramUserName = telegramUserName;
            RegisteredAt = registeredAt;
            State = state;
        }
    }
}
