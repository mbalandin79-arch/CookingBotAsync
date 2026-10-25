using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CookingBot.BackgroundTasks
{
    /// <summary>
    /// Базовая периодическая фоновая задача: бесконечно выполняет Execute
    /// и ждёт задержку delay, пока не отменён токен. Ошибка выполнения
    /// логируется в консоль, пауза 1 секунда, цикл продолжается.
    /// Основной конструктор (C# 12): 
    /// delay — период между запусками Execute;
    /// name — имя задачи для сообщений консоли.
    /// </summary>
    public abstract class BackgroundTask(TimeSpan delay, string name) : IBackgroundTask
    {
        /// <summary>
        /// Один «такт» фоновой задачи — полезная работа наследника
        /// (вызывается циклично с интервалом delay).
        /// </summary>
        /// <param name="ct">Токен отмены операции.</param>
        /// <returns>Задача (Task) одного выполнения.</returns>
        protected abstract Task Execute(CancellationToken ct);

        /// <inheritdoc/>
        public async Task Start(CancellationToken ct)
        {
            while(!ct.IsCancellationRequested)
            {
                try
                {
                    Console.WriteLine($"{name}.Execute");
                    await Execute(ct);

                    Console.WriteLine($"{name}.Start delay {delay}");
                    await Task.Delay(delay, ct);
                }
                catch(OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    // нормально завершаемся при отмене
                }
                catch (Exception ex) 
                { 
                    Console.WriteLine($"{name}.Error:{ex}");
                    await Task.Delay(TimeSpan.FromSeconds(1), ct);
                }
            }
        }
    }
}
