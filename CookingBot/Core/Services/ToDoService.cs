using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CookingBot.Core.DataAccess;
using CookingBot.Core.Entities;
using CookingBot.Core.Exceptions;
using Telegram.Bot.Types;

namespace CookingBot.Core.Services
{
    /// <summary>
    /// Сервис рецептов: бизнес-логика над сущностью ToDoItem.
    /// Проверяет лимиты (количество активных рецептов, длина названия,
    /// количество рецептов в разделе) и дубликаты названий,
    /// работает с хранилищем через IToDoRepository.
    /// </summary>
    internal class ToDoService : IToDoService
    {
        private int _maxTasks = 0;
        private int _maxLengthTask = 0;
        private int _maxRecipesPerList = 50;
        private readonly IToDoRepository _toDoRepository;

        /// <inheritdoc/>
        public int MaxRecipeNameLength => _maxLengthTask;

        /// <summary>Создаёт сервис рецептов.</summary>
        /// <param name="toDoRepository">Репозиторий рецептов.</param>
        public ToDoService(IToDoRepository toDoRepository)
        {
            _toDoRepository = toDoRepository;
        }

        /// <summary>Проверяет лимит активных рецептов пользователя; бросает исключение при превышении.</summary>
        /// <param name="userId">Идентификатор пользователя.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <exception cref="TaskCountLimitException">Достигнут лимит активных рецептов.</exception>
        private async Task CheckCountLimitAsync(Guid userId, CancellationToken ct)
        {
            var checkCount = await _toDoRepository.CountActiveAsync(userId, ct);
            if (checkCount >= _maxTasks)
                throw new TaskCountLimitException(_maxTasks);
        }

        /// <summary>Проверяет длину названия рецепта; бросает исключение при превышении.</summary>
        /// <param name="name">Название рецепта.</param>
        /// <exception cref="TaskLengthLimitException">Название длиннее допустимого.</exception>
        private void CheckLengthLimits(string name)
        {
            if (name.Length > _maxLengthTask)
            {
                throw new TaskLengthLimitException(name.Length, _maxLengthTask);
            }
        }

        /// <summary>
        /// Проверяет, что у пользователя нет рецепта с таким названием;
        /// бросает исключение, если дубликат найден.
        /// </summary>
        /// <param name="userId">Идентификатор пользователя.</param>
        /// <param name="name">Название рецепта.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <exception cref="DuplicateTaskException">Рецепт с таким названием уже есть.</exception>
        private async Task CheckDuplicateAsync(Guid userId, string name, CancellationToken ct)
        {
            var checkExist = await _toDoRepository.ExistsByNameAsync(userId, name, ct);
            if (checkExist)
            {
                throw new DuplicateTaskException(name);
            }
        }

        /// <inheritdoc/>
        public async Task<ToDoItem> AddAsync(ToDoUser user, string name, ToDoItem.MainCategory category, 
            List<string> ingredients, List<string> hiddenIngredients, List<string> steps, ToDoList? todoList, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Имя задачи не может быть пустым.", nameof(name));

            name = name.Trim();

            await CheckCountLimitAsync(user.UserId, ct);
            CheckLengthLimits(name);
            await CheckDuplicateAsync(user.UserId, name, ct);
            if (todoList != null)
            {
                var listItems = await GetByUserIdAndList(user.UserId, todoList.Id, ct);
                if (listItems.Count >= _maxRecipesPerList)
                {
                    throw new TaskCountLimitException(_maxRecipesPerList);
                }
            }

            var item = new ToDoItem
            {
                Id = Guid.NewGuid(),
                User = user,
                Name = name,
                Steps = steps,
                CreatedAt = DateTime.UtcNow,
                State = ToDoItem.ToDoItemState.Active,
                Category = category,
                Ingredients = ingredients,
                HiddenIngredients = hiddenIngredients,
                List = todoList
            };
            await _toDoRepository.AddAsync(item, ct);
            return item;
        }

        /// <inheritdoc/>
        public async Task DeleteAsync(Guid id, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (id == default(Guid))
                return;

            var item = await _toDoRepository.GetAsync(id, ct);
            if (item != null)
            {
                await _toDoRepository.DeleteAsync(id, ct);
            }
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ToDoItem>> GetActiveByUserIdAsync(Guid userId, CancellationToken ct)
        {
            return await _toDoRepository.GetActiveByUserIdAsync(userId, ct);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ToDoItem>> GetAllByUserIdAsync(Guid userId, CancellationToken ct)
        {
            return await _toDoRepository.GetAllByUserIdAsync(userId, ct);
        }

        /// <inheritdoc/>
        public async Task MarkCompletedAsync(Guid id, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (id == default(Guid))
                return;

            var item = await _toDoRepository.GetAsync(id, ct);
            if (item != null)
            {
                item.State = ToDoItem.ToDoItemState.Completed;
                item.StateChangedAt = DateTime.UtcNow; // универсальная дата и время на данный момент для всех часовых поясов
                await _toDoRepository.UpdateAsync(item, ct);
            }
        }

        /// <inheritdoc/>
        public async Task SetConfigurationAsync(int maxTasks, int maxLengthTask, int maxRecipesPerList, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _maxTasks = maxTasks;
            _maxLengthTask = maxLengthTask;
            _maxRecipesPerList = maxRecipesPerList;
            await Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ToDoItem>> FindAsync(ToDoUser user, string namePrefix, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var prefix = namePrefix.ToLower();
            return await _toDoRepository.FindAsync(user.UserId, x => x.Name.ToLower().StartsWith(prefix), ct);
        }

        /// <inheritdoc/>
        public async Task ChangeContentAsync(Guid id, string text, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (id == default(Guid))
                return;

            var item = await _toDoRepository.GetAsync(id, ct);
            if (item != null)
            {
                item.Steps = new List<string> { text };
                await _toDoRepository.UpdateAsync(item, ct);
            }
        }

        /// <inheritdoc/>
        public async Task<ToDoItem?> GetTaskAsync(Guid id, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return await _toDoRepository.GetAsync(id, ct);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ToDoItem>> GetAllTasksAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return await _toDoRepository.GetAllAsync(ct);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ToDoItem>> FindAllAsync(string namePrefix, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var prefix = namePrefix.ToLower();
            return await _toDoRepository.FindAllAsync(x => x.Name.ToLower().StartsWith(prefix), ct);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ToDoItem>> FindByIngredientAsync(string ingredient, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return await _toDoRepository.FindByIngredientAsync(ingredient, ct);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ToDoItem>> FindByCategoryAsync(ToDoItem.MainCategory category, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return await _toDoRepository.FindByCategoryAsync(category, ct);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ToDoItem>> FindByNameContainsAsync(string namePart, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return await _toDoRepository.FindByNameContainsAsync(namePart, ct);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ToDoItem>> GetByUserIdAndList(Guid userId, Guid? listId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var items = await _toDoRepository.GetAllByUserIdAsync(userId, ct);

            if(listId == null)
                return items;

            return items.Where(w => w.List?.Id == listId).ToList();
        }
    }
}
