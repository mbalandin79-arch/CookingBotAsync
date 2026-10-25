using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CookingBot.Core.Exceptions
{
    /// <summary>
    /// Исключение: превышен лимит количества разделов кулинарной книги у пользователя.
    /// Выбрасывается сервисом ToDoListService при создании нового раздела.
    /// </summary>
    public class ListCountLimitException : Exception
    {
        /// <summary>Максимально допустимое количество разделов.</summary>
        public int ListCountLimit { get; }

        /// <summary>
        /// Создаёт исключение для превышенного лимита разделов.
        /// </summary>
        /// <param name="listCountLimit">Максимально допустимое количество разделов.</param>
        public ListCountLimitException(int listCountLimit) : base($"Превышено максимальное количество списков: {listCountLimit}.")
        {
            ListCountLimit = listCountLimit;
        }
    }
}
