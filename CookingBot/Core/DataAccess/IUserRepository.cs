using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CookingBot.Core.Entities;

namespace CookingBot.Core.DataAccess
{
    /// <summary>
    /// Репозиторий пользователей: доступ к хранилищу (БД) для сущности ToDoUser.
    /// Реализация — SqlUserRepository (linq2db).
    /// </summary>
    public interface IUserRepository
    {
        /// <summary>Возвращает пользователя по идентификатору (первичный ключ).</summary>
        /// <param name="userId">Идентификатор пользователя.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Пользователь; null, если пользователя с таким идентификатором нет.</returns>
        Task<ToDoUser?> GetUserByUserIdAsync(Guid userId, CancellationToken ct);

        /// <summary>
        /// Возвращает пользователя по идентификатору в Telegram.
        /// Используется при обработке обновлений для определения текущего пользователя.
        /// </summary>
        /// <param name="telegramUserId">Идентификатор пользователя в Telegram.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Пользователь; null, если пользователь не зарегистрирован.</returns>
        Task<ToDoUser?> GetUserByTelegramUserIdAsync(long telegramUserId, CancellationToken ct);

        /// <summary>Возвращает список всех зарегистрированных пользователей.</summary>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Все пользователи.</returns>
        Task<IReadOnlyList<ToDoUser>> GetAllUsersAsync(CancellationToken ct);

        /// <summary>Добавляет пользователя в хранилище.</summary>
        /// <param name="user">Пользователь для добавления.</param>
        /// <param name="ct">Токен отмены операции.</param>
        Task AddAsync(ToDoUser user, CancellationToken ct);

        /// <summary>
        /// Удаляет пользователя по идентификатору.
        /// Разделы и рецепты пользователя удаляются каскадно (FK ON DELETE CASCADE в БД).
        /// </summary>
        /// <param name="userId">Идентификатор пользователя.</param>
        /// <param name="ct">Токен отмены операции.</param>
        Task DeleteAsync(Guid userId, CancellationToken ct);

        /// <summary>Изменяет существующего пользователя.</summary>
        /// <param name="user">Пользователь с новыми значениями (обновление по ключу UserId).</param>
        /// <param name="ct">Токен отмены операции.</param>
        Task UpdateAsync(ToDoUser user, CancellationToken ct);
    }
}
