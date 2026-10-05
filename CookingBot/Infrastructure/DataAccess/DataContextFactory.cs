using CookingBot.Core.DataAccess;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public DataContextFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        public ToDoDataContext CreateDataContext()
        {
            return new ToDoDataContext(_connectionString);
        }
    }
}
