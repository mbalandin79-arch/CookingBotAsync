using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CookingBot.Core.Entities;

namespace CookingBot.Core.DataAccess
{
    /// <summary>
    /// Репозиторий рецептов: доступ к хранилищу (БД) для сущности ToDoItem.
    /// Реализация — SqlToDoRepository (linq2db).
    /// </summary>
    public interface IToDoRepository
    {
        /// <summary>
        /// Возвращает все рецепты всех пользователей.
        /// По замыслу проекта, незарегистрированный пользователь (гость)
        /// имеет возможность только просмотра всех рецептов.
        /// </summary>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Все рецепты, упорядоченные по дате создания.</returns>
        Task<IReadOnlyList<ToDoItem>> GetAllAsync(CancellationToken ct);

        /// <summary>
        /// Возвращает все рецепты всех пользователей, удовлетворяющие предикату.
        /// Предикат вычисляется в памяти (не транслируется в SQL).
        /// </summary>
        /// <param name="predicate">Условие отбора рецептов.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Рецепты, удовлетворяющие условию.</returns>
        Task<IReadOnlyList<ToDoItem>> FindAllAsync(Func<ToDoItem, bool> predicate, CancellationToken ct);

        /// <summary>Возвращает все рецепты пользователя.</summary>
        /// <param name="userId">Идентификатор пользователя.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Рецепты пользователя, упорядоченные по дате создания.</returns>
        Task<IReadOnlyList<ToDoItem>> GetAllByUserIdAsync(Guid userId, CancellationToken ct);

        /// <summary>Возвращает рецепты пользователя со статусом Active.</summary>
        /// <param name="userId">Идентификатор пользователя.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Активные рецепты пользователя, упорядоченные по дате создания.</returns>
        Task<IReadOnlyList<ToDoItem>> GetActiveByUserIdAsync(Guid userId, CancellationToken ct);

        /// <summary>
        /// Возвращает активные рецепты пользователя, у которых дедлайн
        /// попадает в интервал [from, to). Рецепты без дедлайна (Deadline = null)
        /// не возвращаются. Используется фоновыми задачами: 
        /// поиск просроченных дедлайнов и рецептов «на сегодня».
        /// </summary>
        /// <param name="userId">Идентификатор пользователя.</param>
        /// <param name="from">Начало интервала (включительно), в UTC.</param>
        /// <param name="to">Конец интервала (не включительно), в UTC.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Активные рецепты пользователя с дедлайном в интервале, упорядоченные по дате создания.</returns>
        Task<IReadOnlyList<ToDoItem>> GetActiveWithDeadline(Guid userId, DateTime from, DateTime to, CancellationToken ct);

        /// <summary>
        /// Возвращает рецепты пользователя, удовлетворяющие предикату.
        /// Предикат вычисляется в памяти (не транслируется в SQL).
        /// </summary>
        /// <param name="userId">Идентификатор пользователя.</param>
        /// <param name="predicate">Условие отбора рецептов.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Рецепты пользователя, удовлетворяющие условию.</returns>
        Task<IReadOnlyList<ToDoItem>> FindAsync(Guid userId, Func<ToDoItem, bool> predicate, CancellationToken ct);

        /// <summary>Возвращает рецепт по идентификатору.</summary>
        /// <param name="id">Идентификатор рецепта.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Рецепт; null, если рецепта с таким идентификатором нет.</returns>
        Task<ToDoItem?> GetAsync(Guid id, CancellationToken ct);

        /// <summary>Добавляет рецепт в хранилище.</summary>
        /// <param name="item">Рецепт для добавления.</param>
        /// <param name="ct">Токен отмены операции.</param>
        Task AddAsync(ToDoItem item, CancellationToken ct);

        /// <summary>Изменяет существующий рецепт.</summary>
        /// <param name="item">Рецепт с новыми значениями (обновление по ключу Id).</param>
        /// <param name="ct">Токен отмены операции.</param>
        Task UpdateAsync(ToDoItem item, CancellationToken ct);

        /// <summary>Удаляет рецепт по идентификатору.</summary>
        /// <param name="id">Идентификатор рецепта.</param>
        /// <param name="ct">Токен отмены операции.</param>
        Task DeleteAsync(Guid id, CancellationToken ct);

        /// <summary>Проверяет, есть ли рецепт с таким названием у пользователя (без учёта регистра).</summary>
        /// <param name="userId">Идентификатор пользователя.</param>
        /// <param name="name">Название рецепта.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>true, если рецепт с таким названием у пользователя есть.</returns>
        Task<bool> ExistsByNameAsync(Guid userId, string name, CancellationToken ct);

        /// <summary>Возвращает количество активных рецептов у пользователя.</summary>
        /// <param name="userId">Идентификатор пользователя.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Количество рецептов со статусом Active.</returns>
        Task<int> CountActiveAsync(Guid userId, CancellationToken ct);

        /// <summary>Поиск по всем рецептам по ингредиенту (список Ingredients), без учёта регистра.</summary>
        /// <param name="ingredient">Ингредиент для поиска.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Рецепты, содержащие указанный ингредиент.</returns>
        Task<IReadOnlyList<ToDoItem>> FindByIngredientAsync(string ingredient, CancellationToken ct);

        /// <summary>Поиск по всем рецептам по категории.</summary>
        /// <param name="category">Категория рецепта.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Рецепты указанной категории, упорядоченные по дате создания.</returns>
        Task<IReadOnlyList<ToDoItem>> FindByCategoryAsync(ToDoItem.MainCategory category, CancellationToken ct);

        /// <summary>Поиск по всем рецептам по частичному совпадению названия (без учёта регистра).</summary>
        /// <param name="namePart">Часть названия рецепта.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Рецепты, название которых содержит указанную часть.</returns>
        Task<IReadOnlyList<ToDoItem>> FindByNameContainsAsync(string namePart, CancellationToken ct);
    }
}
