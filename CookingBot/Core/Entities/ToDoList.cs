using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static CookingBot.Core.Entities.ToDoItem;

namespace CookingBot.Core.Entities
{
    public class ToDoList
    {
        public Guid Id { get; }
        public string Name { get; set; }
        public ToDoUser User { get; }
        public DateTime CreatedAt { get; }

        public ToDoList(ToDoUser user, string name)
        {
            Id = Guid.NewGuid();
            User = user;
            Name = name;
            CreatedAt = DateTime.UtcNow;
        }

        [JsonConstructor]
        public ToDoList(Guid id, ToDoUser user, string name, DateTime createdAt)
        {
            Id = id;
            User = user;
            Name = name;
            CreatedAt = createdAt;
        }
    }
}
