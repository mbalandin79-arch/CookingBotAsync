using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CookingBot.Core.DataAccess;
using CookingBot.Core.Entities;
using Otus.ToDoList.ConsoleBot.Types;

namespace CookingBot.Infrastructure.DataAccess
{
    internal class InMemoryUserRepository : IUserRepository
    {
        private List<ToDoUser> _usersInMemory = new List<ToDoUser>();
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public async Task AddAsync(ToDoUser user, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(ct);
            try
            {
                _usersInMemory.Add(user);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task DeleteAsync(Guid userId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(ct);
            try
            {
                var user = _usersInMemory.FirstOrDefault(w => w.UserId == userId);
                if (user != null)
                {
                    _usersInMemory.Remove(user);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<ToDoUser?> GetUserByUserIdAsync(Guid userId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(ct);
            try
            {
                return _usersInMemory.FirstOrDefault(curr => curr.UserId == userId);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<ToDoUser?> GetUserByTelegramUserIdAsync(long telegramUserId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(ct);
            try
            {
                return _usersInMemory.FirstOrDefault(curr => curr.TelegramUserId == telegramUserId);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task UpdateAsync(ToDoUser user, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(ct);
            try
            {
                var index = _usersInMemory.FindIndex(f => f.UserId == user.UserId);
                if (index >= 0)
                {
                    _usersInMemory[index] = user;
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
