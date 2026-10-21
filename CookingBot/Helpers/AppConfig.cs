using System;
using System.Collections.Generic;
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
        /// </summary>
        /// <param name="settingsPath">Полный путь к appsettings.json.</param>
        /// <returns>Кортеж из четырёх лимитов.</returns>
        public static (int maxTasks, int maxLengthTask, int maxListsPerUser, int maxRecipesPerList) ReadLimits(string settingsPath)
        {
            var json = File.ReadAllText(settingsPath);
            using var doc = JsonDocument.Parse(json);

            int maxTasks = doc.RootElement.TryGetProperty("MaxTasks", out var mt) ? mt.GetInt32() : 100;
            int maxLengthTask = doc.RootElement.TryGetProperty("MaxLengthTask", out var mlt) ? mlt.GetInt32() : 100;
            int maxListsPerUser = doc.RootElement.TryGetProperty("MaxListsPerUser", out var ml) ? ml.GetInt32() : 10;
            int maxRecipesPerList = doc.RootElement.TryGetProperty("MaxRecipesPerList", out var mr) ? mr.GetInt32() : 50;

            return (maxTasks, maxLengthTask, maxListsPerUser, maxRecipesPerList);
        }

        /// <summary>
        /// Читает строку подключения к PostgreSQL из appsettings.json.
        /// Возвращает null, если свойства ConnectionString в файле нет.
        /// </summary>
        /// <param name="settingsPath">Полный путь к appsettings.json.</param>
        public static string? ReadConnectionString(string settingsPath)
        {
            var json = File.ReadAllText(settingsPath);
            using var doc = JsonDocument.Parse(json);

            return doc.RootElement.TryGetProperty("ConnectionString", out var cs) ? cs.GetString() : null;
        }
    }
}
