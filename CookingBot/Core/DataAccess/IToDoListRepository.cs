using CookingBot.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CookingBot.Core.Entities.ToDoItem;

namespace CookingBot.Core.DataAccess
{
    public interface IToDoListRepository
    {
        // Если списка нет, то возвращает NULL
        Task<ToDoList?> GetAsync(Guid id, CancellationToken ct);

        Task<IReadOnlyList<ToDoList>> GetByUserIdAsync(Guid userId, CancellationToken ct);

        Task AddAsync(ToDoList todoList, CancellationToken ct);

        Task DeleteAsync(Guid id, CancellationToken ct);

        // Проверяет, есть ли у пользователя список с таким именем
        Task<bool> ExistsByNameAsync(Guid userId, string name, CancellationToken ct);
    }
}
