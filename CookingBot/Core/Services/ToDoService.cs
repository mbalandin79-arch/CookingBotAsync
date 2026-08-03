using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CookingBot.Core.DataAccess;
using CookingBot.Core.Entities;
using CookingBot.Core.Exceptions;
using Otus.ToDoList.ConsoleBot.Types;

namespace CookingBot.Core.Services
{
    internal class ToDoService : IToDoService
    {
        private int _maxTasks = 0;
        private int _maxLengthTask = 0;
        private readonly IToDoRepository _toDoRepository;

        public ToDoService(IToDoRepository toDoRepository)
        {
            _toDoRepository = toDoRepository;
        }

        private async Task CheckCounthLimitAsync(Guid userId)
        {
            if (await _toDoRepository.CountActiveAsync(userId) >= _maxTasks)
                throw new TaskCountLimitException(_maxTasks);
        }

        private void CheckLengthLimits(string name)
        {
            if (name.Length > _maxLengthTask)
            {
                throw new TaskLengthLimitException(name.Length, _maxLengthTask);
            }
        }

        private async Task CheckDuplicateAsync(Guid userId, string name)
        {
            if (await _toDoRepository.ExistsByNameAsync(userId, name))
            {
                throw new DuplicateTaskException(name);
            }
        }

        public async Task<ToDoItem> AddAsync(ToDoUser user, string name)
        {
            await CheckCounthLimitAsync(user.UserId);
            if (name.Length > 0)
            {
                CheckLengthLimits(name);
                await CheckDuplicateAsync(user.UserId, name);
            }

            var item = new ToDoItem(user, name);
            await _toDoRepository.AddAsync(item);
            return item;
        }

        public async Task DeleteAsync(Guid id)
        {
            await _toDoRepository.DeleteAsync(id);
        }

        public async Task<IReadOnlyList<ToDoItem>> GetActiveByUserIdAsync(Guid userId)
        {
            return await _toDoRepository.GetActiveByUserIdAsync(userId);
        }

        public async Task<IReadOnlyList<ToDoItem>> GetAllByUserIdAsync(Guid userId)
        {
            return await _toDoRepository.GetAllByUserIdAsync(userId);
        }

        public async Task MarkCompletedAsync(Guid id)
        {
            if (id != default(Guid))
            {
                ToDoItem updateItem = await _toDoRepository.GetAsync(id);
                updateItem.State = ToDoItem.ToDoItemState.Completed;
                updateItem.StateChangedAt = DateTime.UtcNow; // универсальная дата и время на данный момент для всех часовых поясов
                await _toDoRepository.UpdateAsync(updateItem);
            }
        }

        public void ValidateString(string str)
        {
            if (string.IsNullOrWhiteSpace(str))
                throw new ArgumentException($"{str} это значение не соответствует требованиям");
        }

        public int ParseAndValidateInt(string str, int min, int max)
        {
            int answ = 0;

            if (!int.TryParse(str, out answ) || answ < min || answ > max)
                throw new ArgumentException($"{str} это значение не соответствует требованиям");
            return answ;
        }

        public async Task SetConfigurationAsync(int maxTasks, int maxLengthTask)
        {
            _maxTasks = maxTasks;
            _maxLengthTask = maxLengthTask;
        }

        public async Task<IReadOnlyList<ToDoItem>> FindAsync(ToDoUser user, string namePrefix)
        {
            return await _toDoRepository.FindAsync(user.UserId, x => x.Name.ToLower().StartsWith(namePrefix.ToLower()));
        }
    }
}
