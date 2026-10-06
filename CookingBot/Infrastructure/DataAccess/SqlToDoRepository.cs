using CookingBot.Core.DataAccess;
using CookingBot.Core.Entities;
using LinqToDB;
using LinqToDB.Async;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CookingBot.Infrastructure.DataAccess
{
    internal class SqlToDoRepository : IToDoRepository
    {
        private readonly IDataContextFactory<ToDoDataContext> _factory;

        public SqlToDoRepository(IDataContextFactory<ToDoDataContext> factory)
        {
            _factory = factory;
        }

        public async Task AddAsync(ToDoItem item, CancellationToken ct)
        {
            using var dbContext = _factory.CreateDataContext();

            var model = ModelMapper.MapToModel(item);

            await dbContext.InsertAsync(model, token: ct);
        }

        public async Task<int> CountActiveAsync(Guid userId, CancellationToken ct)
        {
            using var dbContext = _factory.CreateDataContext();

            var answer = await dbContext.ToDoItems.CountAsync(c => c.UserId == userId && c.State == ToDoItem.ToDoItemState.Active, ct);

            return answer;
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct)
        {
            using var dbContext = _factory.CreateDataContext();

            await dbContext.ToDoItems.DeleteAsync(d => d.Id == id, ct);
        }

        public async Task<bool> ExistsByNameAsync(Guid userId, string name, CancellationToken ct)
        {
            using var dbContext = _factory.CreateDataContext();

            var answer = await dbContext.ToDoItems.AnyAsync(a => a.UserId == userId && a.Name.ToLower() == name.ToLower(), ct);

            return answer;
        }

        public async Task<IReadOnlyList<ToDoItem>> FindAllAsync(Func<ToDoItem, bool> predicate, CancellationToken ct)
        {
            var items = await GetAllAsync(ct);

            var answer = items.Where(predicate).ToList();

            return answer;
        }

        public async Task<IReadOnlyList<ToDoItem>> FindAsync(Guid userId, Func<ToDoItem, bool> predicate, CancellationToken ct)
        {
            var items = await GetAllByUserIdAsync(userId, ct);

            var answer = items.Where(predicate).ToList();

            return answer;
        }

        public async Task<IReadOnlyList<ToDoItem>> FindByCategoryAsync(ToDoItem.MainCategory category, CancellationToken ct)
        {
            using var dbContext = _factory.CreateDataContext();

            var models = await dbContext.ToDoItems.LoadWith(l => l.User).LoadWith(l => l.List).LoadWith(l => l.List!.User).Where(w => w.Category == category).OrderBy(o => o.CreatedAt).ToListAsync(ct);

            return models.Select(s => ModelMapper.MapFromModel(s)).ToList();
        }

        public async Task<IReadOnlyList<ToDoItem>> FindByIngredientAsync(string ingredient, CancellationToken ct)
        {
            var answer = await FindAllAsync(item => item.Ingredients != null && item.Ingredients.Any(a => a.Equals(ingredient, StringComparison.OrdinalIgnoreCase)), ct);

            return answer;
        }

        public async Task<IReadOnlyList<ToDoItem>> FindByNameContainsAsync(string namePart, CancellationToken ct)
        {
            using var dbContext = _factory.CreateDataContext();

            var part = namePart.ToLower();

            var models = await dbContext.ToDoItems.LoadWith(l => l.User).LoadWith(l => l.List).LoadWith(l => l.List!.User).Where(w => w.Name.ToLower().Contains(part)).OrderBy(o => o.CreatedAt).ToListAsync(ct);

            return models.Select(s => ModelMapper.MapFromModel(s)).ToList();
        }

        public async Task<IReadOnlyList<ToDoItem>> GetActiveByUserIdAsync(Guid userId, CancellationToken ct)
        {
            using var dbContext = _factory.CreateDataContext();

            var models = await dbContext.ToDoItems.LoadWith(l => l.User).LoadWith(l => l.List).LoadWith(l => l.List!.User).Where(w => w.UserId == userId && w.State == ToDoItem.ToDoItemState.Active).OrderBy(o => o.CreatedAt).ToListAsync(ct);

            return models.Select(s => ModelMapper.MapFromModel(s)).ToList();
        }

        public async Task<IReadOnlyList<ToDoItem>> GetAllAsync(CancellationToken ct)
        {
            using var dbContext = _factory.CreateDataContext();

            var models = await dbContext.ToDoItems.LoadWith(l => l.User).LoadWith(l => l.List).LoadWith(l => l.List!.User).OrderBy(o => o.CreatedAt).ToListAsync(ct);

            return models.Select(s => ModelMapper.MapFromModel(s)).ToList();
        }

        public async Task<IReadOnlyList<ToDoItem>> GetAllByUserIdAsync(Guid userId, CancellationToken ct)
        {
            using var dbContext = _factory.CreateDataContext();

            var models = await dbContext.ToDoItems.LoadWith(l => l.User).LoadWith(l => l.List).LoadWith(l => l.List!.User).Where(w => w.UserId == userId).OrderBy(o => o.CreatedAt).ToListAsync(ct);

            return models.Select(s => ModelMapper.MapFromModel(s)).ToList();
        }

        public async Task<ToDoItem?> GetAsync(Guid id, CancellationToken ct)
        {
            using var dbContext = _factory.CreateDataContext();

            var model = await dbContext.ToDoItems.LoadWith(l => l.User).LoadWith(l => l.List).LoadWith(l => l.List!.User).FirstOrDefaultAsync(f => f.Id == id, ct);

            return model != null ? ModelMapper.MapFromModel(model) : null;
        }

        public async Task UpdateAsync(ToDoItem item, CancellationToken ct)
        {
            using var dbContext = _factory.CreateDataContext();

            var model = ModelMapper.MapToModel(item);

            await dbContext.UpdateAsync(model, token: ct);
        }
    }
}
