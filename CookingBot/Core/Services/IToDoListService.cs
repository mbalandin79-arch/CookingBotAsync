using CookingBot.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CookingBot.Core.Services
{
    public interface IToDoListService
    {
        Task<ToDoList> AddAsync(ToDoUser user, string name, CancellationToken ct);

        Task<ToDoList?> GetAsync(Guid id, CancellationToken ct);

        Task DeleteAsync(Guid id, CancellationToken ct);

        Task<IReadOnlyList<ToDoList>> GetUserListsAsync(Guid userId, CancellationToken ct);

        Task SetConfigurationAsync(int maxListsPerUser, CancellationToken ct);
    }
}
