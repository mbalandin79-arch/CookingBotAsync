using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CookingBot.Core.DataAccess;
using CookingBot.Core.Entities;
using Otus.ToDoList.ConsoleBot.Types;

namespace CookingBot.Infrastructure.DataAccess
{
    internal class InMemoryUserRepository : IUserRepository
    {
        private List<ToDoUser> _usersInMemory = new List<ToDoUser>();

        public async Task AddAsync(ToDoUser user)
        {
            _usersInMemory.Add(user);
        }

        public async Task<ToDoUser?> GetUserAsync(Guid userId)
        {            
            return _usersInMemory.FirstOrDefault(curr => curr.UserId == userId);
        }

        public async Task<ToDoUser?> GetUserByTelegramUserIdAsync(long telegramUserId)
        {            
            return _usersInMemory.FirstOrDefault(curr => curr.TelegramUserId == telegramUserId);
        }
    }
}
