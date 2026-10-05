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
        public Guid Id { get; set; }
        public string Name { get; set; }
        public ToDoUser User { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
