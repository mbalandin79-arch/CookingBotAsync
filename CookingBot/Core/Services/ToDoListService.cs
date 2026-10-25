using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CookingBot.Core.DataAccess;
using CookingBot.Core.Entities;
using CookingBot.Core.Exceptions;

namespace CookingBot.Core.Services
{
    /// <summary>
    /// Сервис разделов кулинарной книги: бизнес-логика над сущностью ToDoList.
    /// Проверяет лимиты (длина названия, количество разделов) и дубликаты названий,
    /// работает с хранилищем через IToDoListRepository.
    /// </summary>
    public class ToDoListService : IToDoListService
    {
        private readonly IToDoListRepository _toDoListRepository;
        private int _maxListsPerUser = 10;

        /// <summary>Максимальная длина названия раздела.</summary>
        public const int MaxListNameLength = 30;

        /// <summary>Создаёт сервис разделов.</summary>
        /// <param name="toDoListRepository">Репозиторий разделов.</param>
        public ToDoListService(IToDoListRepository toDoListRepository)
        {
            _toDoListRepository = toDoListRepository;
        }

        /// <summary>Проверяет длину названия раздела; бросает исключение при превышении.</summary>
        /// <param name="name">Название раздела.</param>
        /// <exception cref="TaskLengthLimitException">Название длиннее MaxListNameLength.</exception>
        private void CheckLengthLimits(string name)
        {
            if (name.Length > MaxListNameLength)
            {
                throw new TaskLengthLimitException(name.Length, MaxListNameLength);
            }
        }

        /// <summary>
        /// Проверяет, что у пользователя нет раздела с таким названием;
        /// бросает исключение, если дубликат найден.
        /// </summary>
        /// <param name="userId">Идентификатор пользователя.</param>
        /// <param name="name">Название раздела.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <exception cref="DuplicateTaskException">Раздел с таким названием уже есть.</exception>
        private async Task CheckDuplicateAsync(Guid userId, string name, CancellationToken ct)
        {
            var checkExist = await _toDoListRepository.ExistsByNameAsync(userId, name, ct);
            if (checkExist)
            {
                throw new DuplicateTaskException(name);
            }
        }

        /// <inheritdoc/>
        public async Task<ToDoList> AddAsync(ToDoUser user, string name, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Имя списка не может быть пустым.", nameof(name));

            name = name.Trim();

            CheckLengthLimits(name);
            await CheckDuplicateAsync(user.UserId, name, ct);
            var existingLists = await _toDoListRepository.GetByUserIdAsync(user.UserId, ct);
            if (existingLists.Count >= _maxListsPerUser)
            {
                throw new ListCountLimitException(_maxListsPerUser);
            }

            var list = new ToDoList
            {
                Id = Guid.NewGuid(),
                User = user,
                Name = name,
                CreatedAt = DateTime.UtcNow
            };
            await _toDoListRepository.AddAsync(list, ct);
            return list;
        }

        /// <inheritdoc/>
        public async Task DeleteAsync(Guid id, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (id == default(Guid))
                return;

            var list = await _toDoListRepository.GetAsync(id, ct);
            if (list != null)
            {
                await _toDoListRepository.DeleteAsync(id, ct);
            }
        }

        /// <inheritdoc/>
        public async Task<ToDoList?> GetAsync(Guid id, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return await _toDoListRepository.GetAsync(id, ct);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ToDoList>> GetUserListsAsync(Guid userId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return await _toDoListRepository.GetByUserIdAsync(userId, ct);
        }

        /// <inheritdoc/>
        public async Task SetConfigurationAsync(int maxListsPerUser, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _maxListsPerUser = maxListsPerUser;
            await Task.CompletedTask;
        }
    }
}
