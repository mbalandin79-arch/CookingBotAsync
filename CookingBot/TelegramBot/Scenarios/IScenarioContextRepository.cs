using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CookingBot.TelegramBot.Scenarios
{
    /// <summary>
    /// Хранилище контекстов сценариев пользователей.
    /// Реализация — InMemoryScenarioContextRepository (в памяти процесса).
    /// </summary>
    public interface IScenarioContextRepository
    {
        /// <summary>Возвращает контекст сценария пользователя.</summary>
        /// <param name="userId">Идентификатор пользователя в Telegram.</param>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Контекст; null, если сценария у пользователя нет.</returns>
        Task<ScenarioContext?> GetContext(long userId, CancellationToken ct);

        /// <summary>Задаёт (заменяет) контекст сценария пользователя.</summary>
        /// <param name="userId">Идентификатор пользователя в Telegram.</param>
        /// <param name="context">Контекст для сохранения.</param>
        /// <param name="ct">Токен отмены операции.</param>
        Task SetContext(long userId, ScenarioContext context, CancellationToken ct);

        /// <summary>Сбрасывает (очищает) контекст сценария пользователя.</summary>
        /// <param name="userId">Идентификатор пользователя в Telegram.</param>
        /// <param name="ct">Токен отмены операции.</param>
        Task ResetContext(long userId, CancellationToken ct);

        /// <summary>
        /// Возвращает все активные контексы сценариев.
        /// Используется фоновой задачей сброса «зависших» сценариев.
        /// </summary>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Все активные контексы (может быть пустой список).</returns>
        Task<IReadOnlyList<ScenarioContext>> GetContexts(CancellationToken ct);
    }
}
