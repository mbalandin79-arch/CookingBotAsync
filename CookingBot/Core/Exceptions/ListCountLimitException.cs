using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CookingBot.Core.Exceptions
{
    public class ListCountLimitException : Exception
    {
        public int ListCountLimit { get; }

        public ListCountLimitException(int listCountLimit) : base($"Превышено максимальное количество списков: {listCountLimit}.")
        {
            ListCountLimit = listCountLimit;
        }
    }
}
