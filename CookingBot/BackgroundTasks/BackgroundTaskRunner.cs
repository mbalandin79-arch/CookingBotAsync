using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CookingBot.BackgroundTasks
{
    public class BackgroundTaskRunner : IDisposable
    {
        private readonly ConcurrentBag<IBackgroundTask> _tasks = new ConcurrentBag<IBackgroundTask>();
        private Task? _runningTasks;
        private CancellationTokenSource? _stoppingCts;

        /// <summary>
        /// Регистрирует задачу для последующего запуска.
        /// </summary>
        /// <exception cref="InvalidOperationException">Tasks are already running</exception>
        public void AddTask(IBackgroundTask task)
        {
            if (_runningTasks is not null)
            {
                throw new InvalidOperationException("Tasks are already running");
            }

            _tasks.Add(task);
        }

        /// <summary>
        /// Запускает зарегистрированные задачи
        /// </summary>
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

        /// <summary>
        /// Останавливает запущенные задачи и ожидает их завершения
        /// </summary>
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

        public void Dispose()
        {
            _stoppingCts?.Cancel();
            _stoppingCts?.Dispose();
        }
    }
}
