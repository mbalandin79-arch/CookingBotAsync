using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CookingBot.TelegramBot.Scenarios
{
    public interface IScenarioContextRepository
    {
        // Получает контекст пользователя
        Task<ScenarioContext?> GetContext(long userId, CancellationToken ct);

        // Задает контекст пользователя
        Task SetContext(long userId, ScenarioContext context, CancellationToken ct);

        // Сбрасывает (очищает) контекст пользователя
        Task ResetContext(long userId, CancellationToken ct);
    }
}
