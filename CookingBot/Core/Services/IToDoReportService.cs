using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CookingBot.Core.Services
{
    public interface IToDoReportService
    {
        Task<(int total, int completed, int active, DateTime generatedAt)> GetUserStatsAsync(Guid userId);
    }
}
