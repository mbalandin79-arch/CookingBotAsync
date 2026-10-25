using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CookingBot.TelegramBot.Scenarios
{
    /// <summary>
    /// Контекст сценария: хранит состояние многошагового диалога пользователя
    /// (какой сценарий активен, на каком шаге, накопленные данные).
    /// Хранится в IScenarioContextRepository, живёт пока сценарий не завершится.
    /// </summary>
    public class ScenarioContext
    {
        /// <summary>Тип сценария: какой диалог выполняется.</summary>
        public enum ScenarioType
        {
            /// <summary>Сценарий не активен.</summary>
            None,
            /// <summary>Добавление рецепта.</summary>
            AddTask,
            /// <summary>Добавление раздела.</summary>
            AddList,
            /// <summary>Удаление раздела.</summary>
            DeleteList,
            /// <summary>Удаление рецепта.</summary>
            DeleteTask
        }

        /// <summary>Результат обработки сообщения сценарием.</summary>
        public enum ScenarioResult
        {
            /// <summary>Переход к следующему шагу. Сообщение обработано, но сценарий ещё не завершён.</summary>
            Transition,
            /// <summary>Сценарий завершён.</summary>
            Completed
        }

        /// <summary>Идентификатор пользователя в Telegram.</summary>
        public long UserId { get; set; }
        /// <summary>Активный сценарий.</summary>
        public ScenarioType CurrentScenario { get; set; }
        /// <summary>Текущий шаг сценария; null — шаг ещё не начат.</summary>
        public string? CurrentStep { get; set; }
        /// <summary>Данные сценария, передаваемые между шагами (ключ — имя значения).</summary>
        public Dictionary<string, object> Data { get; set; }
        /// <summary>Дата и время создания контекста (UTC); используется фоновой задачей сброса «зависших» сценариев.</summary>
        public DateTime CreatedAt { get; }

        /// <summary>Создаёт контекст сценария указанного типа (шаг — null, данные — пустые).</summary>
        /// <param name="scenario">Тип запускаемого сценария.</param>
        public ScenarioContext(ScenarioType scenario) 
        { 
            CurrentScenario = scenario;
            CurrentStep = null;
            Data = new Dictionary<string, object>();
            CreatedAt = DateTime.UtcNow;
        }
    }
}
