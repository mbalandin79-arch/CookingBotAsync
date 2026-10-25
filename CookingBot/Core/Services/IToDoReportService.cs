using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CookingBot.Core.Services
{
    /// <summary>
    /// Сервис статистики: сводка по рецептам пользователя.
    /// Реализация — ToDoReportService.
    /// </summary>
    public interface IToDoReportService
    {
        /// <summary>
        /// Возвращает статистику по рецептам пользователя:
        /// общее количество, завершённые, активные и время формирования.
        /// </summary>
        /// <param name="userId">Идентификатор пользователя.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Кортеж: total — всего рецептов, completed — завершённые, active — активные, generatedAt — время формирования (UTC).</returns>
        Task<(int total, int completed, int active, DateTime generatedAt)> GetUserStatsAsync(Guid userId, CancellationToken ct);
    }
}
