using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CookingBot.Core.Entities;

namespace CookingBot.Core.Services
{
    public interface IUserService
    {
        Task<ToDoUser> RegisterUserAsync(long telegramUserId, string telegramUserName);

        Task<ToDoUser?> GetUserAsync(long telegramUserId);
    }
}
