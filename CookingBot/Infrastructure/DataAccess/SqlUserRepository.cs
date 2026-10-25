using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CookingBot.Core.DataAccess;
using CookingBot.Core.Entities;
using CookingBot.Core.DataAccess.Models;
using LinqToDB;
using LinqToDB.Async;

namespace CookingBot.Infrastructure.DataAccess
{
    /// <summary>
    /// SQL-репозиторий пользователей (реализация IUserRepository на linq2db).
    /// Каждый метод открывает свой контекст БД (через фабрику) и освобождает
    /// его по окончании (using).
    /// </summary>
    internal class SqlUserRepository : IUserRepository
    {
        private readonly IDataContextFactory<ToDoDataContext> _factory;

        /// <summary>Создаёт репозиторий пользователей.</summary>
        /// <param name="factory">Фабрика контекстов БД.</param>
        public SqlUserRepository(IDataContextFactory<ToDoDataContext> factory)
        {
            _factory = factory;
        }

        /// <inheritdoc/>
        public async Task AddAsync(ToDoUser user, CancellationToken ct)
        {
            using var dbContext = _factory.CreateDataContext();

            var model = ModelMapper.MapToModel(user);

            await dbContext.InsertAsync(model, token: ct);
        }

        /// <inheritdoc/>
        public async Task DeleteAsync(Guid userId, CancellationToken ct)
        {
            using var dbContext = _factory.CreateDataContext();

            await dbContext.ToDoUsers.DeleteAsync(d => d.UserId == userId, ct);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ToDoUser>> GetAllUsersAsync(CancellationToken ct)
        {
            using var dbContext = _factory.CreateDataContext();

            var models = await dbContext.ToDoUsers.OrderBy(o => o.TelegramUserName).ToListAsync(ct);

            return models.Select(s => ModelMapper.MapFromModel(s)).ToList();
        }

        /// <inheritdoc/>
        public async Task<ToDoUser?> GetUserByTelegramUserIdAsync(long telegramUserId, CancellationToken ct)
        {
            using var dbContext = _factory.CreateDataContext();

            var model = await dbContext.ToDoUsers.FirstOrDefaultAsync(f => f.TelegramUserId == telegramUserId, ct);

            return model != null ? ModelMapper.MapFromModel(model) : null;
        }

        /// <inheritdoc/>
        public async Task<ToDoUser?> GetUserByUserIdAsync(Guid userId, CancellationToken ct)
        {
            using var dbContext = _factory.CreateDataContext();

            var model = await dbContext.ToDoUsers.FirstOrDefaultAsync(f => f.UserId == userId, ct);

            return model != null ? ModelMapper.MapFromModel(model) : null;
        }

        /// <inheritdoc/>
        public async Task UpdateAsync(ToDoUser user, CancellationToken ct)
        {
            using var dbContext = _factory.CreateDataContext();

            var model = ModelMapper.MapToModel(user);

            await dbContext.UpdateAsync(model, token: ct);
        }
    }
}
