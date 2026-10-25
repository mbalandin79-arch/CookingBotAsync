using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CookingBot.Helpers
{
    /// <summary>
    /// Простейший файловый логгер ошибок: пишет записи в файл
    /// Logs\error_ГГГГ-ММ-ДД.log рядом с исполняемым файлом
    /// (по одному файлу на день). Потокобезопасен (общий lock).
    /// </summary>
    public static class FileLogger
    {
        private static readonly object _lock = new object();
        private static readonly string _logDirectory = Path.Combine(AppContext.BaseDirectory, "Logs");

        /// <summary>
        /// Записывает информацию об исключении в лог-файл текущего дня.
        /// Ошибки самого логирования проглатываются — логирование не должно
        /// ломать работу бота.
        /// </summary>
        /// <param name="ex">Исключение для записи.</param>
        /// <param name="context">Необязательный контекст: где произошла ошибка.</param>
        public static void LogError(Exception ex, string? context = null)
        {
            try
            {
                Directory.CreateDirectory(_logDirectory);
                var logFile = Path.Combine(_logDirectory, $"error_{DateTime.Now:yyyy-MM-dd}.log");

                var lines = new[]
                {
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR",
                    context != null ? $"Context: {context}" : "",
                    $"Type: {ex.GetType().Name}",
                    $"Message: {ex.Message}",
                    $"StackTrace: {ex.StackTrace}",
                    ex.InnerException != null ? $"Inner: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}" : "",
                    new string('-', 80),
                    ""
                };

                lock (_lock)
                {
                    File.AppendAllLines(logFile, lines);
                }
            }
            catch
            {
                // логирование не должно падать
            }
        }
    }
}
