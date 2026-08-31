using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CookingBot.TelegramBot.Dto
{
    public class ToDoListCallbackDto : CallbackDto
    {
        public Guid? ToDoListId { get; set; }

        public static new ToDoListCallbackDto FromString(string input)
        {
            Guid? result = null;
            var parts = input.Split('|');
            if(parts.Length > 1 && Guid.TryParse(parts[1], out var guid)) 
            {
                result = guid;
            }
            
            return new ToDoListCallbackDto
            {
                Action = parts[0],
                ToDoListId = result
            };
        }

        public override string ToString()
        {
            return $"{base.ToString()}|{ToDoListId}";
        }
    }
}
