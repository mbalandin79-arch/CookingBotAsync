using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CookingBot.Core.Exceptions
{
    internal class FindItemInMemoryException : Exception
    {
        public string ItemInMeemory { get; }

        public FindItemInMemoryException(string itemInMeemory) : base()
        {
            ItemInMeemory = itemInMeemory;
        }
    }
}
