using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CookingBot.Core.DataAccess;
using CookingBot.Core.Entities;
using LinqToDB;
using LinqToDB.Async;
using CookingBot.Core.DataAccess.Models;

namespace CookingBot.Infrastructure.DataAccess
{
    internal class SqlUserRepository : IUserRepository
    {
        private readonly IDataContextFactory<ToDoDataContext> _factory;

        public SqlUserRepository(IDataContextFactory<ToDoDataContext> factory)
        {
            _factory = factory;
        }

        public async Task AddAsync(ToDoUser user, CancellationToken ct)
        {
            using var dbContext = _factory.CreateDataContext();

            var model = ModelMapper.MapToModel(user);

            await dbContext.InsertAsync(model, token: ct);
        }

        public async Task DeleteAsync(Guid userId, CancellationToken ct)
        {
            using var dbContext = _factory.CreateDataContext();

            await dbContext.ToDoUsers.DeleteAsync(d => d.UserId == userId, ct);
        }

        public async Task<IReadOnlyList<ToDoUser>> GetAllUsersAsync(CancellationToken ct)
        {
            using var dbContext = _factory.CreateDataContext();

            var models = await dbContext.ToDoUsers.OrderBy(o => o.TelegramUserName).ToListAsync(ct);

            return models.Select(s => ModelMapper.MapFromModel(s)).ToList();
        }

        public async Task<ToDoUser?> GetUserByTelegramUserIdAsync(long telegramUserId, CancellationToken ct)
        {
            using var dbContext = _factory.CreateDataContext();

            var model = await dbContext.ToDoUsers.FirstOrDefaultAsync(f => f.TelegramUserId == telegramUserId, ct);

            return model != null ? ModelMapper.MapFromModel(model) : null;
        }

        public async Task<ToDoUser?> GetUserByUserIdAsync(Guid userId, CancellationToken ct)
        {
            using var dbContext = _factory.CreateDataContext();

            var model = await dbContext.ToDoUsers.FirstOrDefaultAsync(f => f.UserId == userId, ct);

            return model != null ? ModelMapper.MapFromModel(model) : null;
        }

        public async Task UpdateAsync(ToDoUser user, CancellationToken ct)
        {
            using var dbContext = _factory.CreateDataContext();

            var model = ModelMapper.MapToModel(user);

            await dbContext.UpdateAsync(model, token: ct);
        }
    }
}
