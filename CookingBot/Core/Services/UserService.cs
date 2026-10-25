using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CookingBot.Core.DataAccess;
using CookingBot.Core.Entities;

namespace CookingBot.Core.Services
{
    /// <summary>
    /// Сервис пользователей: регистрация, смена имени и уровня доступа,
    /// удаление. Работает с хранилищем через IUserRepository.
    /// </summary>
    internal class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;

        /// <summary>Создаёт сервис пользователей.</summary>
        /// <param name="userRepository">Репозиторий пользователей.</param>
        public UserService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public async Task<ToDoUser?> GetUserAsync(long telegramUserId, CancellationToken ct)
        {
            return await _userRepository.GetUserByTelegramUserIdAsync(telegramUserId, ct);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ToDoUser>> GetAllUsersAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return await _userRepository.GetAllUsersAsync(ct);
        }

        /// <inheritdoc/>
        public async Task<ToDoUser?> GetUserByUserIdAsync(Guid userId, CancellationToken ct)
        {
            return await _userRepository.GetUserByUserIdAsync(userId, ct);
        }

        /// <inheritdoc/>
        public async Task<ToDoUser> RegisterUserAsync(long telegramUserId, string telegramUserName, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var existUser = await _userRepository.GetUserByTelegramUserIdAsync(telegramUserId, ct);
            if (existUser == null)
            {
                var user = new ToDoUser
                {
                    UserId = Guid.NewGuid(),
                    TelegramUserId = telegramUserId,
                    TelegramUserName = telegramUserName,
                    RegisteredAt = DateTime.UtcNow,
                    State = ToDoUser.ToDoUserState.Guest
                };

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
