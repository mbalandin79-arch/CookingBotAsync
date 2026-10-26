using System;
using System.Text;
using System.Threading.Tasks;
using CookingBot.Core.DataAccess;
using CookingBot.Core.Services;

namespace CookingBot.BackgroundTasks
{
    /// <summary>
    /// Фоновая задача: раз в день создаёт для каждого пользователя
    /// нотификацию «рецепты на сегодня» — список активных рецептов, чей
    /// дедлайн приходится на текущую дату (UTC). Отправку созданных
    /// нотификаций выполняет NotificationBackgroundTask.
    /// </summary>
    public class TodayBackgroundTask : BackgroundTask
    {
        private readonly INotificationService _notificationService;
        private readonly IUserRepository _userRepository;
        private readonly IToDoRepository _toDoRepository;

        /// <summary>Создаёт задачу нотификаций «рецепты на сегодня».</summary>
        /// <param name="notificationService">Сервис нотификаций (создание записей; дубль по типу не создаётся).</param>
        /// <param name="userRepository">Репозиторий пользователей (список всех пользователей).</param>
        /// <param name="toDoRepository">Репозиторий рецептов (активные рецепты с дедлайном).</param>
        public TodayBackgroundTask(INotificationService notificationService,
            IUserRepository userRepository, IToDoRepository toDoRepository)
            : base(TimeSpan.FromDays(1), nameof(TodayBackgroundTask))
        {
            _notificationService = notificationService;
            _userRepository = userRepository;
            _toDoRepository = toDoRepository;
        }

        /// <inheritdoc/>
        protected override async Task Execute(CancellationToken ct)
        {
            var users = await _userRepository.GetAllUsersAsync(ct);

            // «сегодня» — интервал [сегодня 00:00 UTC, завтра 00:00 UTC)
            var from = DateTime.UtcNow.Date;
            var to = from.AddDays(1);

            foreach (var user in users)
            {
                var tasks = await _toDoRepository.GetActiveWithDeadline(user.UserId, from, to, ct);

                // нет рецептов на сегодня — нотификация не нужна
                if (tasks.Count == 0)
                {
                    continue;
                }

                // одна нотификация на пользователя за дату: тип Today_{дата}
                // защищает от повторов, в тексте перечислены все рецепты списком
                var text = new StringBuilder();
                text.AppendLine("Рецепты на сегодня:");
                foreach (var task in tasks)
                {
                    text.AppendLine($"- {task.Name}");
                }

                await _notificationService.ScheduleNotification(user.UserId,
                    $"Today_{DateOnly.FromDateTime(DateTime.UtcNow)}",
                    text.ToString(),
                    DateTime.UtcNow, ct);
            }
        }
    }
}
