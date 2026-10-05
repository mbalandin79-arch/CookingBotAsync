using CookingBot.Core.DataAccess.Models;
using LinqToDB;
using LinqToDB.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CookingBot.Infrastructure.DataAccess
{
    /// <summary>
    /// Подключение к БД кулинарной книги.
    /// Свойства ниже — «таблицы» БД, по ним linq2db строит SQL-запросы.
    /// </summary>
    public class ToDoDataContext : DataConnection
    {
        public ToDoDataContext(string connectionString) : base(ProviderName.PostgreSQL, connectionString)
        {
        }

        // GetTable не ходит в БД сам — он лишь возвращает «описание» таблицы.
        // Запрос уходит в PostgreSQL при перечислении: ToList, FirstOrDefault и т.п.
        public ITable<ToDoUserModel> ToDoUsers => this.GetTable<ToDoUserModel>();
        public ITable<ToDoItemModel> ToDoItems => this.GetTable<ToDoItemModel>();
        public ITable<ToDoListModel> ToDoLists => this.GetTable<ToDoListModel>();

    }
}
