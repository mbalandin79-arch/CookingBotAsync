using CookingBot.Core.DataAccess.Models;
using LinqToDB;
using LinqToDB.Data;

namespace CookingBot.Infrastructure.DataAccess
{
    /// <summary>
    /// Подключение к БД кулинарной книги.
    /// Свойства ниже — «таблицы» БД, по ним linq2db строит SQL-запросы.
    /// GetTable не ходит в БД сам — он лишь возвращает «описание» таблицы;
    /// запрос уходит в PostgreSQL при перечислении: ToList, FirstOrDefault и т.п.
    /// </summary>
    public class ToDoDataContext : DataConnection
    {
        /// <summary>Создаёт контекст подключения к PostgreSQL.</summary>
        /// <param name="connectionString">Строка подключения к PostgreSQL.</param>
        public ToDoDataContext(string connectionString) 
            : base(new DataOptions().UseConnectionString(ProviderName.PostgreSQL, connectionString))
        {
        }

        /// <summary>Таблица пользователей БД.</summary>
        public ITable<ToDoUserModel> ToDoUsers => this.GetTable<ToDoUserModel>();

        /// <summary>Таблица рецептов БД.</summary>
        public ITable<ToDoItemModel> ToDoItems => this.GetTable<ToDoItemModel>();

        /// <summary>Таблица разделов БД.</summary>
        public ITable<ToDoListModel> ToDoLists => this.GetTable<ToDoListModel>();

        /// <summary>Таблица нотификаций БД (ДЗ17).</summary>
        public ITable<NotificationModel> Notifications => this.GetTable<NotificationModel>();

    }
}
