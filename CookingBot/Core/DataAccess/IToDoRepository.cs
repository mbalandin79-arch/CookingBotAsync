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
        // Возвращает все задачи всех пользователей
        // По замыслу проекта, незарегистрированный пользователь должен иметь возможность только просмотра всех Рецептов (задач)
        Task<IReadOnlyList<ToDoItem>> GetAllAsync(CancellationToken ct);

        // Возвращает все задачи, которые удовлетворяют предикате
        // Поиск по всем задачам
        // По замыслу проекта, незарегистрированный пользователь должен иметь возможность только просмотра всех Рецептов (задач)
        Task<IReadOnlyList<ToDoItem>> FindAllAsync(Func<ToDoItem, bool> predicate, CancellationToken ct);

        // Возвращает все задачи пользователя для UserId
        Task<IReadOnlyList<ToDoItem>> GetAllByUserIdAsync(Guid userId, CancellationToken ct);

        // Возвращает все задачи пользователя для UserId со статусом Active
        Task<IReadOnlyList<ToDoItem>> GetActiveByUserIdAsync(Guid userId, CancellationToken ct);

        // Возвращает все задачи пользователя, которые удовлетворяют предикате
        Task<IReadOnlyList<ToDoItem>> FindAsync(Guid userId, Func<ToDoItem, bool> predicate, CancellationToken ct);

        // Возвращает задачу по id
        Task<ToDoItem?> GetAsync(Guid id, CancellationToken ct);

        // Добавляет задачу
        Task AddAsync(ToDoItem item, CancellationToken ct);

        // Изменяет существующую задачу
        Task UpdateAsync(ToDoItem item, CancellationToken ct);

        // Удаляет задачу по id
        Task DeleteAsync(Guid id, CancellationToken ct);

        // Проверяет есть ли задача с таким именем у пользователя
        Task<bool> ExistsByNameAsync(Guid userId, string name, CancellationToken ct);

        // Возвращает количество активных задач у пользователя
        Task<int> CountActiveAsync(Guid userId, CancellationToken ct);

        // Поиск по ингредиенту (по списку Ingredients)
        Task<IReadOnlyList<ToDoItem>> FindByIngredientAsync(string ingredient, CancellationToken ct);

        // Поиск по категории
        Task<IReadOnlyList<ToDoItem>> FindByCategoryAsync(ToDoItem.MainCategory category, CancellationToken ct);

        // Поиск по частичному совпадению имени (содержит)
        Task<IReadOnlyList<ToDoItem>> FindByNameContainsAsync(string namePart, CancellationToken ct);
    }
}
