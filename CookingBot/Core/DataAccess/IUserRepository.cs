using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CookingBot.Core.Entities;

namespace CookingBot.Core.DataAccess
{
    public interface IUserRepository
    {
        Task<ToDoUser?> GetUserAsync(Guid userId);

        Task<ToDoUser?> GetUserByTelegramUserIdAsync(long telegramUserId);

        Task AddAsync(ToDoUser user);
    }
}
