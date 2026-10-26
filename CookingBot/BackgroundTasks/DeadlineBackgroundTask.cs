using System;
using System.Threading.Tasks;
using CookingBot.Core.DataAccess;
using CookingBot.Core.Services;

namespace CookingBot.BackgroundTasks
{
    /// <summary>
    /// Фоновая задача: раз в час проверяет активные рецепты,
    /// у которых накануне истёк дедлайн, и создаёт для их авторов
    /// нотификации «Вы пропустили дедлайн». Отправку созданных
    /// нотификаций выполняет NotificationBackgroundTask.
    /// </summary>
    public class DeadlineBackgroundTask : BackgroundTask
    {
        private readonly INotificationService _notificationService;
        private readonly IUserRepository _userRepository;
        private readonly IToDoRepository _toDoRepository;

        /// <summary>Создаёт задачу нотификаций о пропущенных дедлайнах.</summary>
        /// <param name="notificationService">Сервис нотификаций (создание записей; дубль по типу не создаётся).</param>
        /// <param name="userRepository">Репозиторий пользователей (список всех пользователей).</param>
        /// <param name="toDoRepository">Репозиторий рецептов (активные рецепты с дедлайном).</param>
        public DeadlineBackgroundTask(INotificationService notificationService,
            IUserRepository userRepository, IToDoRepository toDoRepository)
            : base(TimeSpan.FromHours(1), nameof(DeadlineBackgroundTask))
        {
            _notificationService = notificationService;
            _userRepository = userRepository;
            _toDoRepository = toDoRepository;
        }

        /// <inheritdoc/>
        protected override async Task Execute(CancellationToken ct)
        {
            var users = await _userRepository.GetAllUsersAsync(ct);

            // дедлайны, истёкшие за последние сутки:
            // интервал [вчера 00:00 UTC, сегодня 00:00 UTC)
            var from = DateTime.UtcNow.AddDays(-1).Date;
            var to = DateTime.UtcNow.Date;

            foreach (var user in users)
            {
                var tasks = await _toDoRepository.GetActiveWithDeadline(user.UserId, from, to, ct);

                foreach (var task in tasks)
                {
                    // создаём нотификацию с немедленной отправкой (ScheduledAt = сейчас);
                    // повторная нотификация по той же задаче не создастся:
                    // ScheduleNotification отклоняет дубль по паре UserId + Type
                    await _notificationService.ScheduleNotification(user.UserId,
                        $"Deadline_{task.Id}",
                        $"Ой! Вы пропустили дедлайн по рецепту {task.Name}",
                        DateTime.UtcNow, ct);
                }
            }
        }
    }
}
