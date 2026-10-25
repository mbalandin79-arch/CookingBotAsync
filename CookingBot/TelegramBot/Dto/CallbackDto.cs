using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CookingBot.TelegramBot.Dto
{
    /// <summary>
    /// Данные callback-запроса inline-кнопки: только действие (без параметров).
    /// Базовый класс для остальных CallbackDto; строка данных кнопки
    /// кодируется через '|' (Action|параметры), отсюда методы FromString/ToString.
    /// </summary>
    public class CallbackDto
    {
        /// <summary>Действие, которое нужно выполнить при нажатии кнопки.</summary>
        public string Action { get; set; } = null!;

        /// <summary>
        /// Разбирает строку данных кнопки ("Action|...") в DTO;
        /// параметры после '|' игнорируются.
        /// </summary>
        /// <param name="input">Строка данных кнопки.</param>
        /// <returns>DTO с заполненным Action.</returns>
        public static CallbackDto FromString(string input)
        {
            var parts = input.Split('|');
            return new CallbackDto
            {
                Action = parts[0]
            };
        }

        /// <summary>Собирает DTO обратно в строку данных кнопки.</summary>
        /// <returns>Строка данных кнопки (только Action).</returns>
        public override string ToString() 
        { 
            return Action; 
        }
    }
}
