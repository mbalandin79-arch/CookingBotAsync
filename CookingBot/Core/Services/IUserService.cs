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

        // Возвращает список всех зарегистрированных пользователей
        Task<IReadOnlyList<ToDoUser>> GetAllUsersAsync(CancellationToken ct);

        // Возвращает информацию о пользователе по userId
        Task<ToDoUser?> GetUserByUserIdAsync(Guid userId, CancellationToken ct);

        // Удаляет пользователя по по userId
        Task DeleteUserByUserIdAsync(Guid userId, CancellationToken ct);

        // Удаляет пользователя по telegramUserId
        Task DeleteUserByTelegramUserIdAsync(long telegramUserId, CancellationToken ct);        

        // Изменяет имя пользователя
        Task ChangeNameUser(Guid userId, string newName, CancellationToken ct);

        // Изменяет статус пользователя
        Task ChangeStateAsync(Guid userId, ToDoUser.ToDoUserState target, CancellationToken ct);
    }
}
