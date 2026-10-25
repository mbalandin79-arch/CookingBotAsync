using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CookingBot.TelegramBot.Dto
{
    /// <summary>
    /// Данные callback-запроса inline-кнопки постраничного списка:
    /// действие + идентификатор раздела + номер страницы.
    /// Формат строки: "Action|ToDoListId|Page" (см. CallbackDto).
    /// </summary>
    public class PagedListCallbackDto : ToDoListCallbackDto
    {
        /// <summary>Номер страницы списка (с нуля).</summary>
        public int Page { get; set; }

        /// <summary>Разбирает строку данных кнопки ("Action|ToDoListId|Page") в DTO.</summary>
        /// <param name="input">Строка данных кнопки.</param>
        /// <returns>DTO с Action, идентификатором раздела и номером страницы.</returns>
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

        /// <summary>Собирает DTO обратно в строку данных кнопки.</summary>
        /// <returns>Строка данных кнопки ("Action|ToDoListId|Page").</returns>
        public override string ToString()
        {
            return $"{base.ToString()}|{Page}";
        }
    }
}
