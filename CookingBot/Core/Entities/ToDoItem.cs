using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using static CookingBot.Core.Entities.ToDoUser;

namespace CookingBot.Core.Entities
{
    public class ToDoItem
    {
        public enum ToDoItemState
        {
            Active,
            Completed
        }

        public Guid Id { get; }
        public ToDoUser User { get; }
        public string Name { get; set; }
        public string? Content { get; set; }
        public DateTime CreatedAt { get; }
        public ToDoItemState State { get; set; }
        public DateTime? StateChangedAt { get; set; }
        public DateTime Deadline { get; set; }

        public ToDoItem(ToDoUser user, string name, DateTime deadline)
        {
            User = user;
            Name = name;
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow; // универсальная дата и время на данный момент для всех часовых поясов
            State = ToDoItemState.Active;
            Deadline = deadline;
        }

        [JsonConstructor]
        public ToDoItem(Guid id, ToDoUser user, string name, string content, DateTime createdAt, ToDoItemState state, DateTime? stateChangedAt, DateTime deadline)
        {
            Id = id;
            User = user;
            Name = name;
            Content = content;
            CreatedAt = createdAt;
            State = state;
            StateChangedAt = stateChangedAt;
            Deadline = deadline;
        }
    }
}
