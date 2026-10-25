using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CookingBot.Core.DataAccess;

namespace CookingBot.Infrastructure.DataAccess
{
    /// <summary>
    /// Фабрика подключений к БД. Создаётся один раз при старте бота,
    /// а репозитории зовут CreateDataContext() для каждого запроса,
    /// чтобы подключение жило недолго (using).
    /// </summary>
    public class DataContextFactory : IDataContextFactory<ToDoDataContext>
    {
        private readonly string _connectionString;

        /// <summary>Создаёт фабрику подключений.</summary>
        /// <param name="connectionString">Строка подключения к PostgreSQL.</param>
        public DataContextFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <inheritdoc/>
        public ToDoDataContext CreateDataContext()
        {
            return new ToDoDataContext(_connectionString);
        }
    }
}
