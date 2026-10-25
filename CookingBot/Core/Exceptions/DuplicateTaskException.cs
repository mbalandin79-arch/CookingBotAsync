using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CookingBot.Core.Exceptions
{
    /// <summary>
    /// Исключение: рецепт с таким названием уже существует у пользователя.
    /// Выбрасывается сервисом ToDoService при добавлении дубликата.
    /// </summary>
    public class DuplicateTaskException : Exception
    {
        /// <summary>Название рецепта, которое оказалось дубликатом.</summary>
        public string Task { get; }

        /// <summary>
        /// Создаёт исключение для дубликата рецепта.
        /// </summary>
        /// <param name="task">Название рецепта, которое оказалось дубликатом.</param>
        public DuplicateTaskException(string task) : base($"Задача '{task}' уже существует.")
        {
            Task = task;
        }
    }
}
