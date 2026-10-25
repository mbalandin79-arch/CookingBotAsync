using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CookingBot.Core.Exceptions
{
    /// <summary>
    /// Исключение: название рецепта превышает максимально допустимую длину.
    /// Выбрасывается сервисом ToDoService при добавлении нового рецепта.
    /// </summary>
    public class TaskLengthLimitException : Exception
    {
        /// <summary>Фактическая длина названия рецепта.</summary>
        public int TaskLength { get; }
        /// <summary>Максимально допустимая длина названия рецепта.</summary>
        public int TaskLengthLimit { get; }

        /// <summary>
        /// Создаёт исключение для слишком длинного названия рецепта.
        /// </summary>
        /// <param name="taskLength">Фактическая длина названия рецепта.</param>
        /// <param name="taskLengthLimit">Максимально допустимая длина названия рецепта.</param>
        public TaskLengthLimitException(int taskLength, int taskLengthLimit) : base($"Длина задачи ({taskLength}) превышает максимально допустимое значение {taskLengthLimit}.")
        {
            TaskLength = taskLength;
            TaskLengthLimit = taskLengthLimit;
        }
    }
}
