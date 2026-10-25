using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CookingBot.Core.Entities;

namespace CookingBot.Core.Services
{
    /// <summary>
    /// Сервис пользователей: регистрация, смена имени и уровня доступа.
    /// Реализация — UserService.
    /// </summary>
    public interface IUserService
    {
        /// <summary>
        /// Регистрирует пользователя: если он ещё не зарегистрирован — создаёт
        /// (с уровнем Guest; самый первый пользователь становится Admin),
        /// если уже зарегистрирован — возвращает существующего.
        /// </summary>
        /// <param name="telegramUserId">Идентификатор пользователя в Telegram.</param>
        /// <param name="telegramUserName">Имя пользователя в Telegram.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Зарегистрированный пользователь.</returns>
        Task<ToDoUser> RegisterUserAsync(long telegramUserId, string telegramUserName, CancellationToken ct);

        /// <summary>Возвращает пользователя по идентификатору в Telegram.</summary>
        /// <param name="telegramUserId">Идентификатор пользователя в Telegram.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Пользователь; null, если пользователь не зарегистрирован.</returns>
        Task<ToDoUser?> GetUserAsync(long telegramUserId, CancellationToken ct);

        /// <summary>Возвращает список всех зарегистрированных пользователей.</summary>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Все пользователи.</returns>
        Task<IReadOnlyList<ToDoUser>> GetAllUsersAsync(CancellationToken ct);

        /// <summary>Возвращает пользователя по идентификатору (первичному ключу).</summary>
        /// <param name="userId">Идентификатор пользователя.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Пользователь; null, если пользователя с таким идентификатором нет.</returns>
        Task<ToDoUser?> GetUserByUserIdAsync(Guid userId, CancellationToken ct);

        /// <summary>
        /// Удаляет пользователя по идентификатору.
        /// Разделы и рецепты пользователя удаляются каскадно (FK ON DELETE CASCADE в БД).
        /// </summary>
        /// <param name="userId">Идентификатор пользователя.</param>
        /// <param name="ct">Токен отмены операции.</param>
        Task DeleteUserByUserIdAsync(Guid userId, CancellationToken ct);

        /// <summary>Изменяет имя пользователя в Telegram.</summary>
        /// <param name="userId">Идентификатор пользователя.</param>
        /// <param name="newName">Новое имя.</param>
        /// <param name="ct">Токен отмены операции.</param>
        Task ChangeNameUser(Guid userId, string newName, CancellationToken ct);

        /// <summary>Изменяет уровень доступа пользователя (роль).</summary>
        /// <param name="userId">Идентификатор пользователя.</param>
        /// <param name="target">Новый уровень доступа.</param>
        /// <param name="ct">Токен отмены операции.</param>
        Task ChangeStateAsync(Guid userId, ToDoUser.ToDoUserState target, CancellationToken ct);
    }
}
