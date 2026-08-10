using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CookingBot.Core.DataAccess;
using CookingBot.Core.Entities;

namespace CookingBot.Infrastructure.DataAccess
{
    internal class FileToDoRepository : IToDoRepository
    {
        public Task AddAsync(ToDoItem item, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task<int> CountActiveAsync(Guid userId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task DeleteAsync(Guid id, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task<bool> ExistsByNameAsync(Guid userId, string name, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyList<ToDoItem>> FindAsync(Guid userId, Func<ToDoItem, bool> predicate, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyList<ToDoItem>> GetActiveByUserIdAsync(Guid userId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyList<ToDoItem>> GetAllByUserIdAsync(Guid userId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task<ToDoItem?> GetAsync(Guid id, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task UpdateAsync(ToDoItem item, CancellationToken ct)
        {
            throw new NotImplementedException();
        }
    }
}
