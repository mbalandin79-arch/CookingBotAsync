using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CookingBot.TelegramBot.Dto
{
    /// <summary>
    /// Данные callback-запроса inline-кнопки: действие + идентификатор раздела.
    /// Формат строки: "Action|ToDoListId" (см. CallbackDto).
    /// </summary>
    public class ToDoListCallbackDto : CallbackDto
    {
        /// <summary>Идентификатор раздела; null, если в данных кнопки его не было.</summary>
        public Guid? ToDoListId { get; set; }

        /// <summary>Разбирает строку данных кнопки ("Action|ToDoListId") в DTO.</summary>
        /// <param name="input">Строка данных кнопки.</param>
        /// <returns>DTO с Action и идентификатором раздела (null при отсутствии).</returns>
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

        /// <summary>Собирает DTO обратно в строку данных кнопки.</summary>
        /// <returns>Строка данных кнопки ("Action|ToDoListId").</returns>
        public override string ToString()
        {
            return $"{base.ToString()}|{ToDoListId}";
        }
    }
}
