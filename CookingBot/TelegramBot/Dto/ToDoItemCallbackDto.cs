using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CookingBot.TelegramBot.Dto
{
    /// <summary>
    /// Данные callback-запроса inline-кнопки: действие + идентификатор рецепта.
    /// Формат строки: "Action|ToDoItemId" (см. CallbackDto).
    /// </summary>
    public class ToDoItemCallbackDto : CallbackDto
    {
        /// <summary>Идентификатор рецепта (Guid.Empty, если в данных кнопки его не было).</summary>
        public Guid ToDoItemId { get; set; }

        /// <summary>Разбирает строку данных кнопки ("Action|ToDoItemId") в DTO.</summary>
        /// <param name="input">Строка данных кнопки.</param>
        /// <returns>DTO с Action и идентификатором рецепта.</returns>
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

        /// <summary>Собирает DTO обратно в строку данных кнопки.</summary>
        /// <returns>Строка данных кнопки ("Action|ToDoItemId").</returns>
        public override string ToString()
        {
            return $"{base.ToString()}|{ToDoItemId}";
        }
    }
}
