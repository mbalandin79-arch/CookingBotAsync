using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public string Name { get; }
        public DateTime CreatedAt { get; set; }
        public ToDoItemState State { get; set; }
        public DateTime? StateChangedAt { get; set; }

        public ToDoItem(ToDoUser user, string name)
        {
            User = user;
            Name = name;
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow; // универсальная дата и время на данный момент для всех часовых поясов
            State = ToDoItemState.Active;
        }
    }
}
