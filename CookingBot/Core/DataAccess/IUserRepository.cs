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
        // Возвращает всю информацию о пользователе по userId
        Task<ToDoUser?> GetUserByUserIdAsync(Guid userId, CancellationToken ct);

        // Возвращает всю информацию о пользователе по telegramUserId
        Task<ToDoUser?> GetUserByTelegramUserIdAsync(long telegramUserId, CancellationToken ct);

        // Добавляет пользователя
        Task AddAsync(ToDoUser user, CancellationToken ct);

        // Удаляет пользователя по userId
        Task DeleteAsync(Guid userId, CancellationToken ct);

        // Изменяет существующего пользователя
        Task UpdateAsync(ToDoUser user, CancellationToken ct);
    }
}
