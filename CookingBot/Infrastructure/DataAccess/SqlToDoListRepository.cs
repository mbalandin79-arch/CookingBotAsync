using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CookingBot.Core.DataAccess;
using CookingBot.Core.Entities;
using LinqToDB;
using LinqToDB.Async;

namespace CookingBot.Infrastructure.DataAccess
{
    /// <summary>
    /// SQL-репозиторий разделов (реализация IToDoListRepository на linq2db).
    /// Каждый метод открывает свой контекст БД (через фабрику) и освобождает
    /// его по окончании (using).
    /// </summary>
    internal class SqlToDoListRepository : IToDoListRepository
    {
        private readonly IDataContextFactory<ToDoDataContext> _factory;

        /// <summary>
        /// Создаёт репозиторий разделов.
        /// </summary>
        /// <param name="factory">Фабрика контекстов БД.</param>
        public SqlToDoListRepository(IDataContextFactory<ToDoDataContext> factory)
        {
            _factory = factory;
        }

        /// <inheritdoc/>
        public async Task AddAsync(ToDoList todoList, CancellationToken ct)
        {
            using var dbContext = _factory.CreateDataContext();

            var model = ModelMapper.MapToModel(todoList);

            await dbContext.InsertAsync(model, token: ct);
        }

        /// <inheritdoc/>
        public async Task DeleteAsync(Guid id, CancellationToken ct)
        {
            using var dbContext = _factory.CreateDataContext();

            await dbContext.ToDoLists.DeleteAsync(d => d.Id == id, ct);
        }

        /// <inheritdoc/>
        public async Task<bool> ExistsByNameAsync(Guid userId, string name, CancellationToken ct)
        {
            using var dbContext = _factory.CreateDataContext();

            var answer = await dbContext.ToDoLists.AnyAsync(a => a.UserId == userId && a.Name.ToLower() == name.ToLower(), ct);

            return answer;
        }

        /// <inheritdoc/>
        public async Task<ToDoList?> GetAsync(Guid id, CancellationToken ct)
        {
            using var dbContext = _factory.CreateDataContext();

            var model = await dbContext.ToDoLists.LoadWith(l => l.User).FirstOrDefaultAsync(f => f.Id == id, ct);

            return model != null ? ModelMapper.MapFromModel(model) : null;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ToDoList>> GetByUserIdAsync(Guid userId, CancellationToken ct)
        {
            using var dbContext = _factory.CreateDataContext();

            var models = await dbContext.ToDoLists.LoadWith(l => l.User).Where(w => w.UserId == userId).OrderBy(o => o.CreatedAt).ToListAsync(ct);

            return models.Select(s => ModelMapper.MapFromModel(s)).ToList();
        }
    }
}
