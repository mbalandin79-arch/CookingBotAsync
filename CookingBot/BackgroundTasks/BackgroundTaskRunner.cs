using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CookingBot.BackgroundTasks
{
    /// <summary>
    /// Раннер фоновых задач: принимает задачи (AddTask), запускает их все
    /// (StartTasks) и корректно останавливает с ожиданием завершения
    /// (StopTasks). Используется в Program.cs: запуск до старта бота,
    /// остановка после основного цикла. Dispose дополнительно отменяет
    /// внутренний токен.
    /// </summary>
    public class BackgroundTaskRunner : IDisposable
    {
        private readonly ConcurrentBag<IBackgroundTask> _tasks = new ConcurrentBag<IBackgroundTask>();
        private Task? _runningTasks;
        private CancellationTokenSource? _stoppingCts;

        /// <summary>Регистрирует задачу для последующего запуска.</summary>
        /// <param name="task">Фоновая задача для регистрации.</param>
        /// <exception cref="InvalidOperationException">Tasks are already running</exception>
        public void AddTask(IBackgroundTask task)
        {
            if (_runningTasks is not null)
            {
                throw new InvalidOperationException("Tasks are already running");
            }

            _tasks.Add(task);
        }

        /// <summary>Запускает зарегистрированные задачи</summary>
        /// <param name="ct">Токен отмены: его отмена останавливает все задачи.</param>
        /// <exception cref="InvalidOperationException">Tasks are already running</exception>
        public void StartTasks(CancellationToken ct)
        {
            if (_runningTasks is not null)
            {
                throw new InvalidOperationException("Tasks are already running");
            }

            _stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            // Отдельная обёртка для логирования и корректной обработки отмены
            static async Task RunSafe(IBackgroundTask task, CancellationToken ct)
            {
                try
                {
                    await task.Start(ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    // нормально завершаемся при отмене
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error in {task.GetType().Name}:{ex}");
                }
            }

            // собираем все tasks в один
            _runningTasks = Task.WhenAll(_tasks.Select(t => RunSafe(t, _stoppingCts.Token)));
        }

        /// <summary>Останавливает запущенные задачи и ожидает их завершения</summary>
        /// <param name="ct">Токен ожидания завершения задач.</param>
        public async Task StopTasks(CancellationToken ct)
        {
            if (_runningTasks is null)
            {
                return;
            }

            try
            {
                _stoppingCts?.Cancel();
            }
            finally
            {
                await ((Task)_runningTasks).WaitAsync(ct).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
        }

        /// <summary>Останавливает задачи (отмена внутреннего токена) и освобождает ресурсы.</summary>
        public void Dispose()
        {
            _stoppingCts?.Cancel();
            _stoppingCts?.Dispose();
        }
    }
}
