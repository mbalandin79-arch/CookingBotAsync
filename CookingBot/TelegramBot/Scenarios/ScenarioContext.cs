using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CookingBot.TelegramBot.Scenarios
{
    public class ScenarioContext
    {
        public enum ScenarioType
        {
            None, 
            AddTask,
            AddList,
            DeleteList
        }
        public enum ScenarioResult
        {
            Transition, // Переход к следующему шагу. Сообщение обработано, но сценарий еще не завершен
            Completed   // Сценарий завершен
        }
        public long UserId { get; set; }    // Id пользователя в Telegram
        public ScenarioType CurrentScenario { get; set; }
        public string? CurrentStep { get; set; }
        public Dictionary<string, object> Data { get; set; }

        public ScenarioContext(ScenarioType scenario) 
        { 
            CurrentScenario = scenario;
            CurrentStep = null;
            Data = new Dictionary<string, object>();
        }
    }
}
