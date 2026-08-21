using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CookingBot.Core.Entities;

namespace CookingBot.Core.Services
{
    public interface IToDoService
    {
        // Возвращает все задачи для UserId
        Task<IReadOnlyList<ToDoItem>> GetAllByUserIdAsync(Guid userId, CancellationToken ct);

        // Возвращает задачи для UserId со статусом Active
        Task<IReadOnlyList<ToDoItem>> GetActiveByUserIdAsync(Guid userId, CancellationToken ct);

        // Возвращает информацию о задаче по id
        Task<ToDoItem?> GetTaskAsync(Guid id, CancellationToken ct);

        // Добавляет задачи в общий Список и возвращает добавленную задачу
        Task<ToDoItem> AddAsync(ToDoUser user, string name, DateTime deadline, CancellationToken ct);

        // Изменяет состояние задачи в общем Списоке по id с Active на Completed 
        Task MarkCompletedAsync(Guid id, CancellationToken ct);

        // Изменяет Content у задачи по id
        Task ChangeContentAsync(Guid id, string text, CancellationToken ct);

        // Удаляет задачу из общего Списока по id
        Task DeleteAsync(Guid id, CancellationToken ct);

        // Задает ограничения для задач
        Task SetConfigurationAsync(int maxTasks, int maxLengthTask, CancellationToken ct);

        // Возвращает все задачи пользователя, которые начинаются на namePrefix, использует метод IToDoRepository.Find
        Task<IReadOnlyList<ToDoItem>> FindAsync(ToDoUser user, string namePrefix, CancellationToken ct);
    }
}
