using System;
using System.Linq;
using System.Threading.Tasks;
using CookingBot.Core.DataAccess;
using CookingBot.Core.DataAccess.Models;
using CookingBot.Core.Entities;
using CookingBot.Core.Services;
using LinqToDB;
using LinqToDB.Async;

namespace CookingBot.Infrastructure.DataAccess
{
    /// <summary>
    /// SQL-реализация INotificationService на linq2db.
    /// Работает с БД напрямую, без репозиториев (по заданию): 
    /// каждый метод открывает свой контекст БД через фабрику и освобождает его (using).
    /// </summary>
    public class NotificationService : INotificationService
    {
        private readonly IDataContextFactory<ToDoDataContext> _factory;

        /// <summary>Создаёт сервис нотификаций.</summary>
        /// <param name="factory">Фабрика контекстов БД.</param>
        public NotificationService(IDataContextFactory<ToDoDataContext> factory)
        {
            _factory = factory;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<Notification>> GetScheduledNotification(DateTime scheduledBefore, CancellationToken ct)
        {
            using var dbContext = _factory.CreateDataContext();

            var models = await dbContext.Notifications.LoadWith(l => l.User).Where(w => !w.IsNotified && w.ScheduledAt <= scheduledBefore).ToListAsync(ct);

            return models.Select(ModelMapper.MapFromModel).ToList();
        }

        /// <inheritdoc/>
        public async Task MarkNotified(Guid notificationId, CancellationToken ct)
        {
            using var dbContext = _factory.CreateDataContext();

            var model = await dbContext.Notifications.FirstOrDefaultAsync(f => f.Id == notificationId, ct);
            if (model == null)
            {
                return;
            }

            model.IsNotified = true;
            model.NotifiedAt = DateTime.UtcNow;

            await dbContext.UpdateAsync(model, token: ct);
        }

        /// <inheritdoc/>
        public async Task<bool> ScheduleNotification(Guid userId, string type, string text, DateTime scheduledAt, CancellationToken ct)
        {
            using var dbContext = _factory.CreateDataContext();

            // защита от дублей: запись с такой парой userId + type уже есть
            var exists = await dbContext.Notifications.AnyAsync(a => a.UserId == userId && a.Type == type, ct);
            if (exists)
            {
                return false;
            }

            var model = new NotificationModel
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Type = type,
                Text = text,
                ScheduledAt = scheduledAt,
                IsNotified = false,
                NotifiedAt = null
            };

            await dbContext.InsertAsync(model, token: ct);
            return true;
        }
    }
}
