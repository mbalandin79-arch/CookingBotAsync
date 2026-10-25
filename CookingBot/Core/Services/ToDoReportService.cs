using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using CookingBot.Core.DataAccess;
using CookingBot.Core.Entities;

namespace CookingBot.Core.Services
{
    /// <summary>
    /// Сервис статистики: сводка по рецептам пользователя.
    /// Считает общее количество, активные и завершённые рецепты
    /// на основе данных IToDoService.
    /// </summary>
    internal class ToDoReportService : IToDoReportService
    {
        private readonly IToDoService _todoService;

        /// <summary>Создаёт сервис статистики.</summary>
        /// <param name="todoService">Сервис рецептов (источник данных).</param>
        public ToDoReportService(IToDoService todoService)
        {
            _todoService = todoService;
        }

        /// <inheritdoc/>
        public async Task<(int total, int completed, int active, DateTime generatedAt)> GetUserStatsAsync(Guid userId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            DateTime _generateAt = DateTime.UtcNow;

            var allByUser = await _todoService.GetAllByUserIdAsync(userId, ct);
            var activeByUser = await _todoService.GetActiveByUserIdAsync(userId, ct);

            int _total = allByUser.Count;
            int _active = activeByUser.Count;
            int _completed = _total - _active;

            return (total: _total, completed: _completed, active: _active, generatedAt: _generateAt);
        }
    }
}
