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

        public async Task ChangeNameUser(Guid userId, string newName, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (userId == default(Guid))
                return;

            var user = await _userRepository.GetUserByUserIdAsync(userId, ct);
            if (user != null)
            {
                user.TelegramUserName = newName;
                await _userRepository.UpdateAsync(user, ct);
            }
        }

        public async Task ChangeStateAsync(Guid userId, ToDoUser.ToDoUserState target, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (userId == default(Guid))
                return;

            var user = await _userRepository.GetUserByUserIdAsync(userId, ct);
            if (user != null)
            {
                user.State = target;
                await _userRepository.UpdateAsync(user, ct);
            }
        }
                
        public async Task DeleteUserByTelegramUserIdAsync(long telegramUserId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (telegramUserId <= 0)
                return;

            var user = await _userRepository.GetUserByTelegramUserIdAsync(telegramUserId, ct);
            if (user != null)
            {
                await _userRepository.DeleteAsync(user.UserId, ct);
            }
        }

        public async Task DeleteUserByUserIdAsync(Guid userId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (userId == default(Guid))
                return;

            var user = await _userRepository.GetUserByUserIdAsync(userId, ct);
            if (user != null)
            {
                await _userRepository.DeleteAsync(user.UserId, ct);
            }
        }

        public async Task<ToDoUser?> GetUserAsync(long telegramUserId, CancellationToken ct) // поиск Пользователя в БД и возврат всей записи пользователя либо NULL
        {
            return await _userRepository.GetUserByTelegramUserIdAsync(telegramUserId, ct);
        }

        public async Task<IReadOnlyList<ToDoUser>> GetAllUsersAsync(CancellationToken ct) // поиск Пользователя в БД и возврат всей записи пользователя либо NULL
        {
            ct.ThrowIfCancellationRequested();
            return await _userRepository.GetAllUsersAsync(ct);
        }

        public async Task<ToDoUser?> GetUserByUserIdAsync(Guid userId, CancellationToken ct)
        {
            return await _userRepository.GetUserByUserIdAsync(userId, ct);
        }

        public async Task<ToDoUser> RegisterUserAsync(long telegramUserId, string telegramUserName, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var existUser = await _userRepository.GetUserByTelegramUserIdAsync(telegramUserId, ct);
            if (existUser == null)
            {
                var user = new ToDoUser(telegramUserId, telegramUserName);

                // если это первый пользователь, то он Админ
                var allUsers = await _userRepository.GetAllUsersAsync(ct);
                if (allUsers.Count == 0)
                    user.State = ToDoUser.ToDoUserState.Admin;

                await _userRepository.AddAsync(user, ct);
                return user;
            }
            return existUser;
        }
    }
}
