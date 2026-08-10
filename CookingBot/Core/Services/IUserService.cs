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
        // Добавляет нового пользователя и возвращает информацию о пользователе
        Task<ToDoUser> RegisterUserAsync(long telegramUserId, string telegramUserName, CancellationToken ct);

        // Возвращает информацию о пользователе по telegramUserId
        Task<ToDoUser?> GetUserAsync(long telegramUserId, CancellationToken ct);

        // Возвращает информацию о пользователе по userId
        Task<ToDoUser?> GetUserByUserIdAsync(Guid userId, CancellationToken ct);

        // Удаляет пользователя по по userId
        Task DeleteUserByUserIdAsync(Guid userId, CancellationToken ct);

        // Удаляет пользователя по telegramUserId
        Task DeleteUserByTelegramUserIdAsync(long telegramUserId, CancellationToken ct);

        // Изменяет статус пользователя с Guest на Member
        Task ChangeStateUserFromGuestToMember(Guid userId, CancellationToken ct);

        // Изменяет статус пользователя с Member на Guest
        Task ChangeStateUserFromMemberToGuest(Guid userId, CancellationToken ct);

        // Изменяет статус пользователя с Member на Advanced
        Task ChangeStateUserFromMemberToAdvanced(Guid userId, CancellationToken ct);

        // Изменяет статус пользователя с Advanced на Member
        Task ChangeStateUserFromAdvancedToMember(Guid userId, CancellationToken ct);

        // Изменяет статус пользователя с Advanced на Moderator
        Task ChangeStateUserFromAdvancedToModerator(Guid userId, CancellationToken ct);

        // Изменяет статус пользователя с Moderator на Advanced
        Task ChangeStateUserFromModeratorToAdvanced(Guid userId, CancellationToken ct);

        // Изменяет статус пользователя с Moderator на Admin
        Task ChangeStateUserFromModeratorToAdmin(Guid userId, CancellationToken ct);

        // Изменяет статус пользователя с Admin на Moderator
        Task ChangeStateUserFromAdminToModerator(Guid userId, CancellationToken ct);

        // Изменяет имя пользователя
        Task ChangeNameUser(Guid userId, string newName, CancellationToken ct);
    }
}
