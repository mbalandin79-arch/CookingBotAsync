using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using CookingBot.Core.DataAccess;
using CookingBot.Core.Entities;

namespace CookingBot.Core.Services
{
    internal class ToDoReportService : IToDoReportService
    {
        private readonly IToDoService _todoService;

        public ToDoReportService(IToDoService todoService)
        {
            _todoService = todoService;
        }

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
