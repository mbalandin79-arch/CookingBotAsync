using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CookingBot.Core.DataAccess;
using CookingBot.Core.Entities;

namespace CookingBot.Core.Services
{
    internal class UserService : IUserService
    {        
        private readonly IUserRepository _userRepository;

        public UserService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<ToDoUser?> GetUserAsync(long telegramUserId) // поиск Пользователя в БД и возврат всей записи пользователя либо NULL
        {
            return await _userRepository.GetUserByTelegramUserIdAsync(telegramUserId);
        }

        public async Task<ToDoUser> RegisterUserAsync(long telegramUserId, string telegramUserName)
        {
            var user = new ToDoUser(telegramUserId, telegramUserName);
            await _userRepository.AddAsync(user);
            return user;
        }
    }
}
