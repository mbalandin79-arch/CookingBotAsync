using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CookingBot.Core.DataAccess;
using CookingBot.Core.Entities;
using CookingBot.Core.Exceptions;
using Telegram.Bot.Types;

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

        private async Task CheckCountLimitAsync(Guid userId, CancellationToken ct)
        {
            var checkCount = await _toDoRepository.CountActiveAsync(userId, ct);
            if (checkCount >= _maxTasks)
                throw new TaskCountLimitException(_maxTasks);
        }

        private void CheckLengthLimits(string name)
        {
            if (name.Length > _maxLengthTask)
            {
                throw new TaskLengthLimitException(name.Length, _maxLengthTask);
            }
        }

        private async Task CheckDuplicateAsync(Guid userId, string name, CancellationToken ct)
        {
            var checkExist = await _toDoRepository.ExistsByNameAsync(userId, name, ct);
            if (checkExist)
            {
                throw new DuplicateTaskException(name);
            }
        }

        public async Task<ToDoItem> AddAsync(ToDoUser user, string name, DateTime deadline, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Имя задачи не может быть пустым.", nameof(name));

            name = name.Trim();

            await CheckCountLimitAsync(user.UserId, ct);
            CheckLengthLimits(name);
            await CheckDuplicateAsync(user.UserId, name, ct);

            var item = new ToDoItem(user, name, deadline);
            await _toDoRepository.AddAsync(item, ct);
            return item;
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (id == default(Guid))
                return;

            var item = await _toDoRepository.GetAsync(id, ct);
            if (item != null)
            {
                await _toDoRepository.DeleteAsync(id, ct);
            }
        }

        public async Task<IReadOnlyList<ToDoItem>> GetActiveByUserIdAsync(Guid userId, CancellationToken ct)
        {
            return await _toDoRepository.GetActiveByUserIdAsync(userId, ct);
        }

        public async Task<IReadOnlyList<ToDoItem>> GetAllByUserIdAsync(Guid userId, CancellationToken ct)
        {
            return await _toDoRepository.GetAllByUserIdAsync(userId, ct);
        }

        public async Task MarkCompletedAsync(Guid id, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (id == default(Guid))
                return;

            var item = await _toDoRepository.GetAsync(id, ct);
            if (item != null)
            {
                item.State = ToDoItem.ToDoItemState.Completed;
                item.StateChangedAt = DateTime.UtcNow; // универсальная дата и время на данный момент для всех часовых поясов
                await _toDoRepository.UpdateAsync(item, ct);
            }
        }

        public async Task SetConfigurationAsync(int maxTasks, int maxLengthTask, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _maxTasks = maxTasks;
            _maxLengthTask = maxLengthTask;
            await Task.CompletedTask;
        }

        public async Task<IReadOnlyList<ToDoItem>> FindAsync(ToDoUser user, string namePrefix, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var prefix = namePrefix.ToLower();
            return await _toDoRepository.FindAsync(user.UserId, x => x.Name.ToLower().StartsWith(prefix), ct);
        }

        public async Task ChangeContentAsync(Guid id, string text, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (id == default(Guid))
                return;

            var item = await _toDoRepository.GetAsync(id, ct);
            if (item != null)
            {
                item.Content = text;
                await _toDoRepository.UpdateAsync(item, ct);
            }
        }

        public async Task<ToDoItem?> GetTaskAsync(Guid id, CancellationToken ct)
        {
            return await _toDoRepository.GetAsync(id, ct);
        }
    }
}
