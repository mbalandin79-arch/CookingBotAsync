using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CookingBot.Core.DataAccess;
using CookingBot.Core.Entities;
using CookingBot.Core.Exceptions;

namespace CookingBot.Infrastructure.DataAccess
{
    internal class InMemoryToDoRepository : IToDoRepository
    {
        private List<ToDoItem> _itemsInMemory = new List<ToDoItem>();
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public async Task AddAsync(ToDoItem item, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(ct);
            try
            {
                _itemsInMemory.Add(item);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<int> CountActiveAsync(Guid userId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(ct);
            try
            {
                return _itemsInMemory.Count(w => w.User.UserId == userId && w.State == ToDoItem.ToDoItemState.Active);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(ct);
            try
            {
                var item = _itemsInMemory.FirstOrDefault(w => w.Id == id);
                if (item != null)
                {
                    _itemsInMemory.Remove(item);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<bool> ExistsByNameAsync(Guid userId, string name, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(ct);
            try
            {
                return _itemsInMemory.Any(curr => curr.User.UserId == userId && string.Equals(curr.Name, name, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<ToDoItem?> GetAsync(Guid id, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(ct);
            try
            {
                return _itemsInMemory.FirstOrDefault(curr => curr.Id == id);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<IReadOnlyList<ToDoItem>> GetActiveByUserIdAsync(Guid userId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(ct);
            try
            {
                return _itemsInMemory.Where(w => w.User.UserId == userId && w.State == ToDoItem.ToDoItemState.Active).ToList();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<IReadOnlyList<ToDoItem>> GetAllByUserIdAsync(Guid userId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(ct);
            try
            {
                return _itemsInMemory.Where(w => w.User.UserId == userId).ToList();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task UpdateAsync(ToDoItem item, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(ct);
            try
            {
                var index = _itemsInMemory.FindIndex(f => f.Id == item.Id);
                if (index >= 0)
                {
                    _itemsInMemory[index] = item;
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<IReadOnlyList<ToDoItem>> FindAsync(Guid userId, Func<ToDoItem, bool> predicate, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(ct);
            try
            {
                return _itemsInMemory.Where(f => predicate(f) && f.User.UserId == userId).ToList();
            }
            finally
            {
                _semaphore.Release();
            }            
        }
    }
}
