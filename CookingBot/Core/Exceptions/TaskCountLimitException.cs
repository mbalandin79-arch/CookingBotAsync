using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CookingBot.Core.Exceptions
{
    /// <summary>
    /// Исключение: превышен лимит количества рецептов у пользователя.
    /// Выбрасывается сервисом ToDoService при добавлении нового рецепта.
    /// </summary>
    public class TaskCountLimitException : Exception
    {
        /// <summary>Максимально допустимое количество рецептов.</summary>
        public int TaskCountLimit { get; }

        /// <summary>
        /// Создаёт исключение для превышенного лимита рецептов.
        /// </summary>
        /// <param name="taskCountLimit">Максимально допустимое количество рецептов.</param>
        public TaskCountLimitException(int taskCountLimit) : base($"Превышено максимальное количество задач: {taskCountLimit}.")
        {
            TaskCountLimit = taskCountLimit;
        }
    }
}
