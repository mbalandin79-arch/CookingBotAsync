using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CookingBot.Core.Entities;

namespace CookingBot.Core.DataAccess
{
    public interface IToDoRepository
    {
        // Возвращает все задачи пользователя для UserId
        Task<IReadOnlyList<ToDoItem>> GetAllByUserIdAsync(Guid userId);

        // Возвращает все задачи пользователя для UserId со статусом Active
        Task<IReadOnlyList<ToDoItem>> GetActiveByUserIdAsync(Guid userId);

        // Возвращает все задачи пользователя, которые удовлетворяют предикате
        Task<IReadOnlyList<ToDoItem>> FindAsync(Guid userId, Func<ToDoItem, bool> predicate);

        Task<ToDoItem?> GetAsync(Guid id);

        Task AddAsync(ToDoItem item);

        Task UpdateAsync(ToDoItem item);

        Task DeleteAsync(Guid id);

        // Проверяет есть ли задача с таким именем у пользователя
        Task<bool> ExistsByNameAsync(Guid userId, string name);

        // Возвращает количество активных задач у пользователя
        Task<int> CountActiveAsync(Guid userId);
    }
}
