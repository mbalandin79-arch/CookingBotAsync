using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CookingBot.Helpers
{
    /// <summary>
    /// Чтение настроек из appsettings.json.
    /// Единственное место в программе, которое знает, как хранятся лимиты
    /// и строка подключения: остальной код просит «прочитай мне лимиты»
    /// и не заботится о формате файла.
    /// </summary>
    public class AppConfig
    {
        /// <summary>
        /// Читает лимиты из appsettings.json.
        /// Если какого-то поля нет — используется значение по умолчанию
        /// (EnsureConfigurationAsync при старте дополняет недостающие поля).
        /// Лимиты рецептов и разделов зависят от роли пользователя
        /// (Member/Advanced; Moderator/Admin — без ограничений).
        /// </summary>
        /// <param name="settingsPath">Полный путь к appsettings.json.</param>
        /// <returns>Кортеж из шести лимитов.</returns>
        public static (int maxTasksMember, int maxTasksAdvanced, int maxLengthTask, 
            int maxListsPerUserMember, int maxListsPerUserAdvanced, int maxRecipesPerList) ReadLimits(string settingsPath)
        {
            var json = File.ReadAllText(settingsPath);
            using var doc = JsonDocument.Parse(json);

            int maxTasksMember = doc.RootElement.TryGetProperty("MaxTasksMember", out var mtm) ? mtm.GetInt32() : 100;
            int maxTasksAdvanced = doc.RootElement.TryGetProperty("MaxTasksAdvanced", out var mta) ? mta.GetInt32() : 200;
            int maxLengthTask = doc.RootElement.TryGetProperty("MaxLengthTask", out var mlt) ? mlt.GetInt32() : 100;
            int maxListsPerUserMember = doc.RootElement.TryGetProperty("MaxListsPerUserMember", out var mlm) ? mlm.GetInt32() : 10;
            int maxListsPerUserAdvanced = doc.RootElement.TryGetProperty("MaxListsPerUserAdvanced", out var mla) ? mla.GetInt32() : 20;
            int maxRecipesPerList = doc.RootElement.TryGetProperty("MaxRecipesPerList", out var mr) ? mr.GetInt32() : 50;

            return (maxTasksMember, maxTasksAdvanced, maxLengthTask, maxListsPerUserMember, maxListsPerUserAdvanced, maxRecipesPerList);
        }

        /// <summary>Читает строку подключения к PostgreSQL из appsettings.json.</summary>
        /// <param name="settingsPath">Полный путь к appsettings.json.</param>
        /// <returns>Строка подключения; null, если свойства ConnectionString в файле нет.</returns>
        public static string? ReadConnectionString(string settingsPath)
        {
            var json = File.ReadAllText(settingsPath);
            using var doc = JsonDocument.Parse(json);

            return doc.RootElement.TryGetProperty("ConnectionString", out var cs) ? cs.GetString() : null;
        }

        /// <summary>
        /// Читает настройки фоновых задач из appsettings.json (значения — целые часы).
        /// Если какого-то поля нет — используется значение по умолчанию
        /// (EnsureConfigurationAsync при старте дополняет недостающие поля).
        /// </summary>
        /// <param name="settingsPath">Полный путь к appsettings.json.</param>
        /// <returns>Кортеж: интервал запуска задачи сброса сценариев (ч) и таймаут сценария (ч).</returns>
        public static (int resetScenarioRunIntervalHours, int resetScenarioTimeoutHours) ReadBackgroundTasks(string settingsPath)
        {
            var json = File.ReadAllText(settingsPath);
            using var doc = JsonDocument.Parse(json);

            int runIntervalHours = doc.RootElement.TryGetProperty("ResetScenarioRunIntervalHours", out var ri) ? ri.GetInt32() : 1;
            int timeoutHours = doc.RootElement.TryGetProperty("ResetScenarioTimeoutHours", out var t) ? t.GetInt32() : 1;

            return (runIntervalHours, timeoutHours);
        }
    }
}
