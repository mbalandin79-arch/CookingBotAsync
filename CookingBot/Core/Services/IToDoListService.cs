using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CookingBot.Core.Entities;
using CookingBot.Core.Exceptions;

namespace CookingBot.Core.Services
{
    /// <summary>
    /// Сервис разделов кулинарной книги: бизнес-логика над сущностью ToDoList
    /// (проверки лимитов и дубликатов). Реализация — ToDoListService.
    /// </summary>
    public interface IToDoListService
    {
        /// <summary>Создаёт раздел с указанным названием у пользователя.</summary>
        /// <param name="user">Пользователь — владелец раздела.</param>
        /// <param name="name">Название раздела (обрезается по краям).</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Созданный раздел.</returns>
        /// <exception cref="ArgumentException">Название пустое или состоит из пробелов.</exception>
        /// <exception cref="TaskLengthLimitException">Название длиннее допустимого (30 символов).</exception>
        /// <exception cref="DuplicateTaskException">Раздел с таким названием у пользователя уже есть.</exception>
        /// <exception cref="ListCountLimitException">Достигнут лимит количества разделов.</exception>
        Task<ToDoList> AddAsync(ToDoUser user, string name, CancellationToken ct);

        /// <summary>Возвращает раздел по идентификатору.</summary>
        /// <param name="id">Идентификатор раздела.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Раздел; null, если раздела с таким идентификатором нет.</returns>
        Task<ToDoList?> GetAsync(Guid id, CancellationToken ct);

        /// <summary>
        /// Удаляет раздел по идентификатору; если раздела нет — ничего не делает.
        /// Рецепты раздела удаляются каскадно.
        /// </summary>
        /// <param name="id">Идентификатор раздела.</param>
        /// <param name="ct">Токен отмены операции.</param>
        Task DeleteAsync(Guid id, CancellationToken ct);

        /// <summary>Возвращает все разделы пользователя.</summary>
        /// <param name="userId">Идентификатор пользователя.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Разделы пользователя (может быть пустой список).</returns>
        Task<IReadOnlyList<ToDoList>> GetUserListsAsync(Guid userId, CancellationToken ct);

        /// <summary>Устанавливает максимальное количество разделов на пользователя.</summary>
        /// <param name="maxListsPerUser">Максимальное количество разделов.</param>
        /// <param name="ct">Токен отмены операции.</param>
        Task SetConfigurationAsync(int maxListsPerUser, CancellationToken ct);
    }
}
