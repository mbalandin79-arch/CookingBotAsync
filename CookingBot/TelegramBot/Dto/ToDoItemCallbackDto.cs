using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CookingBot.TelegramBot.Dto
{
    public class ToDoItemCallbackDto : CallbackDto
    {
        public Guid ToDoItemId { get; set; }

        public static new ToDoItemCallbackDto FromString(string input) 
        {
            var parts = input.Split('|');
            var dto = new ToDoItemCallbackDto
            {
                Action = parts[0]
            };

            if (parts.Length > 1 && Guid.TryParse(parts[1], out var id))
            {
                dto.ToDoItemId = id;
            }

            return dto;
        }

        public override string ToString()
        {
            return $"{base.ToString()}|{ToDoItemId}";
        }
    }
}
