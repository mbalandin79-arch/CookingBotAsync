using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CookingBot.Core.Entities;
using static CookingBot.Core.Entities.ToDoItem;

namespace CookingBot.Core.DataAccess
{
    /// <summary>
    /// Репозиторий разделов кулинарной книги: доступ к хранилищу (БД)
    /// для сущности ToDoList. Реализация — SqlToDoListRepository (linq2db).
    /// </summary>
    public interface IToDoListRepository
    {
        /// <summary>Возвращает раздел по идентификатору.</summary>
        /// <param name="id">Идентификатор раздела.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Раздел; null, если раздела с таким идентификатором нет.</returns>
        Task<ToDoList?> GetAsync(Guid id, CancellationToken ct);

        /// <summary>Возвращает все разделы пользователя.</summary>
        /// <param name="userId">Идентификатор пользователя.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Список разделов пользователя (может быть пустым).</returns>
        Task<IReadOnlyList<ToDoList>> GetByUserIdAsync(Guid userId, CancellationToken ct);

        /// <summary>Добавляет раздел в хранилище.</summary>
        /// <param name="todoList">Раздел для добавления.</param>
        /// <param name="ct">Токен отмены операции.</param>
        Task AddAsync(ToDoList todoList, CancellationToken ct);

        /// <summary>
        /// Удаляет раздел по идентификатору.
        /// Рецепты раздела удаляются каскадно (FK ON DELETE CASCADE в БД).
        /// </summary>
        /// <param name="id">Идентификатор раздела.</param>
        /// <param name="ct">Токен отмены операции.</param>
        Task DeleteAsync(Guid id, CancellationToken ct);

        /// <summary>Проверяет, есть ли у пользователя раздел с таким названием.</summary>
        /// <param name="userId">Идентификатор пользователя.</param>
        /// <param name="name">Название раздела.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>true, если раздел с таким названием у пользователя есть.</returns>
        Task<bool> ExistsByNameAsync(Guid userId, string name, CancellationToken ct);
    }
}
