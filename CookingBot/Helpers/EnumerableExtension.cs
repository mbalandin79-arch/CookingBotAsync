using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CookingBot.Helpers
{
    public static class EnumerableExtension
    {
        public static IEnumerable<T> GetBatchByNumber<T>(this IEnumerable<T> source, int batchSize, int batchNumber)
        {
            return source.Skip(batchSize * batchNumber).Take(batchSize);
        }
    }
}
