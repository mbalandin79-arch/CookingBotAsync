using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CookingBot.Helpers
{
    /// <summary>Методы расширения для коллекций (IEnumerable).</summary>
    public static class EnumerableExtension
    {
        /// <summary>
        /// Возвращает «страницу» коллекции: пропускает предыдущие страницы
        /// и берёт указанное количество элементов. Используется для постраничного
        /// вывода списков (рецептов, разделов) в сообщениях бота.
        /// </summary>
        /// <typeparam name="T">Тип элементов коллекции.</typeparam>
        /// <param name="source">Исходная коллекция.</param>
        /// <param name="batchSize">Размер страницы (элементов на странице).</param>
        /// <param name="batchNumber">Номер страницы (с нуля).</param>
        /// <returns>Элементы указанной страницы (может быть пусто, если страница за пределами коллекции).</returns>
        public static IEnumerable<T> GetBatchByNumber<T>(this IEnumerable<T> source, int batchSize, int batchNumber)
        {
            return source.Skip(batchSize * batchNumber).Take(batchSize);
        }
    }
}
