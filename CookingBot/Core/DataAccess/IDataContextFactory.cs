using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LinqToDB.Data;

namespace CookingBot.Core.DataAccess
{
    /// <summary>
    /// Фабрика контекстов базы данных.
    /// Создаёт новый экземпляр контекста по требованию: репозитории вызывают
    /// CreateDataContext в каждом методе, обращающемся к БД.
    /// </summary>
    /// <typeparam name="TDataContext">Тип создаваемого контекста (наследник DataConnection).</typeparam>
    public interface IDataContextFactory<TDataContext> where TDataContext : DataConnection
    {
        /// <summary>Создаёт новый экземпляр контекста базы данных.</summary>
        /// <returns>Новый контекст для работы с БД.</returns>
        TDataContext CreateDataContext();
    }
}
