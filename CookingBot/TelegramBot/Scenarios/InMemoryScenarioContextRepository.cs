using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CookingBot.TelegramBot.Scenarios
{
    internal class InMemoryScenarioContextRepository : IScenarioContextRepository
    {
        private readonly Dictionary<long, ScenarioContext> _scenarioContext = new Dictionary<long, ScenarioContext>();

        public InMemoryScenarioContextRepository() { }

        public Task<ScenarioContext?> GetContext(long userId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _scenarioContext.TryGetValue(userId, out var context);
            return Task.FromResult(context);
        }

        public Task SetContext(long userId, ScenarioContext context, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _scenarioContext[userId] = context;
            return Task.CompletedTask;
        }

        public Task ResetContext(long userId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _scenarioContext.Remove(userId);
            return Task.CompletedTask;
        }
    }
}
