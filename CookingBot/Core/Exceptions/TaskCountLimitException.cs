using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CookingBot.Core.Exceptions
{
    public class TaskCountLimitException : Exception
    {
        public int TaskCountLimit { get; }

        public TaskCountLimitException(int taskCountLimit) : base($"Превышено максимальное количество задач: {taskCountLimit}.")
        {
            TaskCountLimit = taskCountLimit;
        }
    }
}
