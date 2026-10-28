using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CookingBot.Core.Entities;
using CookingBot.Core.Exceptions;

namespace CookingBot.Core.Services
{
    /// <summary>
    /// Сервис рецептов: бизнес-логика над сущностью ToDoItem
    /// (проверки лимитов и дубликатов, поиск).
    /// Реализация — ToDoService.
    /// </summary>
    public interface IToDoService
    {
        /// <summary>Максимальная длина названия рецепта (из конфигурации).</summary>
        int MaxRecipeNameLength { get; }
        /// <summary>Возвращает лимит рецептов для роли пользователя.</summary>
        /// <param name="state">Роль пользователя.</param>
        /// <returns>Лимит рецептов; null — для Moderator/Admin (без ограничений).</returns>
        int? GetRecipeLimit(ToDoUser.ToDoUserState state);

        /// <summary>
        /// Возвращает все рецепты всех пользователей.
        /// По замыслу проекта, незарегистрированный пользователь (гость)
        /// имеет возможность только просмотра всех рецептов.
        /// </summary>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Все рецепты.</returns>
        Task<IReadOnlyList<ToDoItem>> GetAllTasksAsync(CancellationToken ct);

        /// <summary>
        /// Возвращает все рецепты всех пользователей, названия которых
        /// начинаются с указанного префикса (без учёта регистра).
        /// </summary>
        /// <param name="namePrefix">Префикс названия рецепта.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Рецепты, начинающиеся с префикса.</returns>
        Task<IReadOnlyList<ToDoItem>> FindAllAsync(string namePrefix, CancellationToken ct);

        /// <summary>Возвращает все рецепты пользователя.</summary>
        /// <param name="userId">Идентификатор пользователя.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Рецепты пользователя.</returns>
        Task<IReadOnlyList<ToDoItem>> GetAllByUserIdAsync(Guid userId, CancellationToken ct);

        /// <summary>Возвращает рецепт по идентификатору.</summary>
        /// <param name="id">Идентификатор рецепта.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Рецепт; null, если рецепта с таким идентификатором нет.</returns>
        Task<ToDoItem?> GetTaskAsync(Guid id, CancellationToken ct);

        /// <summary>Создаёт рецепт у пользователя и возвращает его.</summary>
        /// <param name="user">Автор рецепта.</param>
        /// <param name="name">Название рецепта (обрезается по краям).</param>
        /// <param name="category">Категория рецепта.</param>
        /// <param name="ingredients">Ингредиенты (возможен поиск).</param>
        /// <param name="hiddenIngredients">Скрытые ингредиенты (поиск не возможен).</param>
        /// <param name="steps">Шаги приготовления.</param>
        /// <param name="todoList">Раздел рецепта; null — без раздела.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Созданный рецепт.</returns>
        /// <exception cref="ArgumentException">Название пустое или состоит из пробелов.</exception>
        /// <exception cref="TaskCountLimitException">Достигнут лимит активных рецептов или лимит рецептов в разделе.</exception>
        /// <exception cref="TaskLengthLimitException">Название длиннее допустимого.</exception>
        /// <exception cref="DuplicateTaskException">Рецепт с таким названием у пользователя уже есть.</exception>
        Task<ToDoItem> AddAsync(ToDoUser user, string name, ToDoItem.MainCategory category, List<string> ingredients, List<string> hiddenIngredients, 
            List<string> steps, ToDoList? todoList, CancellationToken ct);

        /// <summary>
        /// Заменяет шаги приготовления рецепта одним текстом;
        /// если рецепта нет — ничего не делает. 
        /// Зарезервировано под будущее редактирование рецепта.
        /// </summary>
        /// <param name="id">Идентификатор рецепта.</param>
        /// <param name="text">Новый текст (шаги приготовления).</param>
        /// <param name="ct">Токен отмены операции.</param>
        Task ChangeContentAsync(Guid id, string text, CancellationToken ct);

        /// <summary>Удаляет рецепт по идентификатору; если рецепта нет — ничего не делает.</summary>
        /// <param name="id">Идентификатор рецепта.</param>
        /// <param name="ct">Токен отмены операции.</param>
        Task DeleteAsync(Guid id, CancellationToken ct);

        /// <summary>Задаёт ограничения для рецептов.</summary>
        /// <param name="maxTasksMember">Максимальное количество рецептов на пользователя с ролью Member.</param>
        /// <param name="maxTasksAdvanced">Максимальное количество рецептов на пользователя с ролью Advanced.</param>
        /// <param name="maxLengthTask">Максимальная длина названия рецепта.</param>
        /// <param name="maxRecipesPerList">Максимальное количество рецептов в разделе.</param>
        /// <param name="ct">Токен отмены операции.</param>
        Task SetConfigurationAsync(int maxTasksMember, int maxTasksAdvanced, int maxLengthTask, int maxRecipesPerList, CancellationToken ct);

        /// <summary>
        /// Возвращает рецепты пользователя, названия которых начинаются
        /// с указанного префикса (без учёта регистра).
        /// </summary>
        /// <param name="user">Пользователь.</param>
        /// <param name="namePrefix">Префикс названия рецепта.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Рецепты пользователя, начинающиеся с префикса.</returns>
        Task<IReadOnlyList<ToDoItem>> FindAsync(ToDoUser user, string namePrefix, CancellationToken ct);

        /// <summary>Поиск по всем рецептам по ингредиенту (без учёта регистра).</summary>
        /// <param name="ingredient">Ингредиент для поиска.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Рецепты, содержащие указанный ингредиент.</returns>
        Task<IReadOnlyList<ToDoItem>> FindByIngredientAsync(string ingredient, CancellationToken ct);

        /// <summary>Поиск по всем рецептам по категории.</summary>
        /// <param name="category">Категория рецепта.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Рецепты указанной категории.</returns>
        Task<IReadOnlyList<ToDoItem>> FindByCategoryAsync(ToDoItem.MainCategory category, CancellationToken ct);

        /// <summary>Поиск по всем рецептам по частичному совпадению названия (без учёта регистра).</summary>
        /// <param name="namePart">Часть названия рецепта.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Рецепты, название которых содержит указанную часть.</returns>
        Task<IReadOnlyList<ToDoItem>> FindByNameContainsAsync(string namePart, CancellationToken ct);

        /// <summary>
        /// Возвращает рецепты пользователя в указанном разделе;
        /// если listId равен null — все рецепты пользователя.
        /// </summary>
        /// <param name="userId">Идентификатор пользователя.</param>
        /// <param name="listId">Идентификатор раздела; null — все рецепты пользователя.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Рецепты пользователя в разделе (или все).</returns>
        Task<IReadOnlyList<ToDoItem>> GetByUserIdAndList(Guid userId, Guid? listId, CancellationToken ct);
    }
}
