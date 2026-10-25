using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CookingBot.BackgroundTasks
{
    /// <summary>
    /// Фоновая задача бота: выполняется периодически, пока бот работает.
    /// Запускается и останавливается через BackgroundTaskRunner (Program.cs).
    /// Реализация — BackgroundTask (периодический цикл) и его наследники
    /// (например, ResetScenarioBackgroundTask).
    /// </summary>
    public interface IBackgroundTask
    {
        /// <summary>
        /// Запускает выполнение фоновой задачи; задача выполняется,
        /// пока не будет отменён токен.
        /// </summary>
        /// <param name="ct">Токен отмены: при отмене задача должна завершиться.</param>
        /// <returns>Задача (Task), завершается при остановке фоновой задачи.</returns>
        Task Start(CancellationToken ct);
    }
}
