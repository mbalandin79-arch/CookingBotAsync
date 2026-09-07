using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CookingBot.TelegramBot.Dto
{
    public class PagedListCallbackDto : ToDoListCallbackDto
    {
        public int Page { get; set; }

        public static new PagedListCallbackDto FromString(string input)
        {
            var parts = input.Split('|');
            var dto = new PagedListCallbackDto
            {
                Action = parts[0]
            };

            if (parts.Length > 1 && Guid.TryParse(parts[1], out var id))
            {
                dto.ToDoListId = id;
            }

            if (parts.Length > 2 && int.TryParse(parts[2], out var page))
            {
                dto.Page = page;
            }

            return dto;
        }

        public override string ToString()
        {
            return $"{base.ToString()}|{Page}";
        }
    }
}
