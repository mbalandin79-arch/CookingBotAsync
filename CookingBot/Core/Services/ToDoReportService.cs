using System;
using System.Threading;
using System.Threading.Tasks;

namespace CookingBot.Core.Services
{
    /// <summary>
    /// Сервис статистики: сводка по рецептам и разделам пользователя
    /// на основе данных IToDoService и IToDoListService.
    /// </summary>
    internal class ToDoReportService : IToDoReportService
    {
        private readonly IToDoService _todoService;
        private readonly IToDoListService _toDoListService;

        /// <summary>Создаёт сервис статистики.</summary>
        /// <param name="todoService">Сервис рецептов (источник данных).</param>
        /// <param name="toDoListService">Сервис разделов (источник данных).</param>
        public ToDoReportService(IToDoService todoService, IToDoListService toDoListService)
        {
            _todoService = todoService;
            _toDoListService = toDoListService;
        }

        /// <inheritdoc/>
        public async Task<(int totalRecipes, int totalLists)> GetUserStatsAsync(Guid userId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var allByUser = await _todoService.GetAllByUserIdAsync(userId, ct);
            var listsByUser = await _toDoListService.GetUserListsAsync(userId, ct);

            return (totalRecipes: allByUser.Count, totalLists: listsByUser.Count);
        }
    }
}
