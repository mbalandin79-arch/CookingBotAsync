using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CookingBot.Core.Exceptions
{
    public class TaskLengthLimitException : Exception
    {
        public int TaskLength { get; }
        public int TaskLengthLimit { get; }

        public TaskLengthLimitException(int taskLength, int taskLengthLimit) : base()
        {
            TaskLength = taskLength;
            TaskLengthLimit = taskLengthLimit;
        }
    }
}
