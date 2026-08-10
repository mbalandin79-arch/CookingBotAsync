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

        private async Task ChangeStateAsync(Guid userId, ToDoUser.ToDoUserState expected, ToDoUser.ToDoUserState target, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (userId == default(Guid))
                return;

            var user = await _userRepository.GetUserByUserIdAsync(userId, ct);
            if (user != null && user.State == expected)
            {
                user.State = target;
                await _userRepository.UpdateAsync(user, ct);
            }
        }

        public async Task ChangeStateUserFromAdminToModerator(Guid userId, CancellationToken ct)
        {
            await ChangeStateAsync(userId, ToDoUser.ToDoUserState.Admin, ToDoUser.ToDoUserState.Moderator, ct);
        }

        public async Task ChangeStateUserFromAdvancedToMember(Guid userId, CancellationToken ct)
        {
            await ChangeStateAsync(userId, ToDoUser.ToDoUserState.Advanced, ToDoUser.ToDoUserState.Member, ct);
        }

        public async Task ChangeStateUserFromAdvancedToModerator(Guid userId, CancellationToken ct)
        {
            await ChangeStateAsync(userId, ToDoUser.ToDoUserState.Advanced, ToDoUser.ToDoUserState.Moderator, ct);
        }

        public async Task ChangeStateUserFromGuestToMember(Guid userId, CancellationToken ct)
        {
            await ChangeStateAsync(userId, ToDoUser.ToDoUserState.Guest, ToDoUser.ToDoUserState.Member, ct);
        }

        public async Task ChangeStateUserFromMemberToAdvanced(Guid userId, CancellationToken ct)
        {
            await ChangeStateAsync(userId, ToDoUser.ToDoUserState.Member, ToDoUser.ToDoUserState.Advanced, ct);
        }

        public async Task ChangeStateUserFromMemberToGuest(Guid userId, CancellationToken ct)
        {
            await ChangeStateAsync(userId, ToDoUser.ToDoUserState.Member, ToDoUser.ToDoUserState.Guest, ct);
        }

        public async Task ChangeStateUserFromModeratorToAdmin(Guid userId, CancellationToken ct)
        {
            await ChangeStateAsync(userId, ToDoUser.ToDoUserState.Moderator, ToDoUser.ToDoUserState.Admin, ct);
        }

        public async Task ChangeStateUserFromModeratorToAdvanced(Guid userId, CancellationToken ct)
        {
            await ChangeStateAsync(userId, ToDoUser.ToDoUserState.Moderator, ToDoUser.ToDoUserState.Advanced, ct);
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
                await _userRepository.AddAsync(user, ct);
                return user;
            }
            return existUser;
        }
    }
}
