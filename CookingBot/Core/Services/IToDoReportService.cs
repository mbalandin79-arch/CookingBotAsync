using System;
using System.Threading;
using System.Threading.Tasks;

namespace CookingBot.Core.Services
{
    /// <summary>
    /// Сервис статистики: сводка по рецептам и разделам пользователя.
    /// Реализация — ToDoReportService.
    /// </summary>
    public interface IToDoReportService
    {
        /// <summary>Возвращает статистику пользователя по рецептам и разделам.</summary>
        /// <param name="userId">Идентификатор пользователя.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Кортеж: всего рецептов (totalRecipes) и всего разделов (totalLists).</returns>
        Task<(int totalRecipes, int totalLists)> GetUserStatsAsync(Guid userId, CancellationToken ct);
    }
}
