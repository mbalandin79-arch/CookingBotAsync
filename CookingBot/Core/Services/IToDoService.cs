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
        // Возвращает все ToDoItem для UserId
        Task<IReadOnlyList<ToDoItem>> GetAllByUserIdAsync(Guid userId);

        // Возвращает ToDoItem для UserId со статусом Active
        Task<IReadOnlyList<ToDoItem>> GetActiveByUserIdAsync(Guid userId);

        // Добавляет ToDoItem в общий Список и возвращает добавленный ToDoItem
        Task<ToDoItem> AddAsync(ToDoUser user, string name);

        // Изменяет ToDoItem в общем Списоке по Guid сотояние с Active на Completed 
        Task MarkCompletedAsync(Guid id);

        // Удаляет ToDoItem из общего Списока по Guid
        Task DeleteAsync(Guid id);

        // Задает ограничения для ToDoItem
        Task SetConfigurationAsync(int maxTasks, int maxLengthTask);

        // Возвращает все задачи пользователя, которые начинаются на namePrefix, использует метод IToDoRepository.Find
        Task<IReadOnlyList<ToDoItem>> FindAsync(ToDoUser user, string namePrefix);
    }
}
