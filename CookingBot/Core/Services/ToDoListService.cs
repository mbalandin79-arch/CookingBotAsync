using CookingBot.Core.DataAccess;
using CookingBot.Core.Entities;
using CookingBot.Core.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CookingBot.Core.Services
{
    public class ToDoListService : IToDoListService
    {
        private readonly IToDoListRepository _toDoListRepository;

        public ToDoListService(IToDoListRepository toDoListRepository)
        {
            _toDoListRepository = toDoListRepository;
        }

        private void CheckLengthLimits(string name)
        {
            if (name.Length > 10)
            {
                throw new TaskLengthLimitException(name.Length, 10);
            }
        }

        private async Task CheckDuplicateAsync(Guid userId, string name, CancellationToken ct)
        {
            var checkExist = await _toDoListRepository.ExistsByNameAsync(userId, name, ct);
            if (checkExist)
            {
                throw new DuplicateTaskException(name);
            }
        }

        public async Task<ToDoList> AddAsync(ToDoUser user, string name, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Имя списка не может быть пустым.", nameof(name));

            name = name.Trim();
                        
            CheckLengthLimits(name);
            await CheckDuplicateAsync(user.UserId, name, ct);

            var list = new ToDoList(user, name);
            await _toDoListRepository.AddAsync(list, ct);
            return list;
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (id == default(Guid))
                return;

            var list = await _toDoListRepository.GetAsync(id, ct);
            if (list != null)
            {
                await _toDoListRepository.DeleteAsync(id, ct);
            }
        }

        public async Task<ToDoList?> GetAsync(Guid id, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return await _toDoListRepository.GetAsync(id, ct);
        }

        public async Task<IReadOnlyList<ToDoList>> GetUserListsAsync(Guid userId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return await _toDoListRepository.GetByUserIdAsync(userId, ct);
        }
    }
}
