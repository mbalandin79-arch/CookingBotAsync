using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CookingBot.TelegramBot.Scenarios
{
    internal class InMemoryScenarioContextRepository : IScenarioContextRepository
    {
        /// <summary>
        /// Хранилище контекстов сценариев в памяти процесса
        /// (реализация IScenarioContextRepository). Использует
        /// ConcurrentDictionary — безопасен при одновременных запросах;
        /// контексты теряются при перезапуске бота.
        /// </summary>
        private readonly ConcurrentDictionary<long, ScenarioContext> _scenarioContext = new ConcurrentDictionary<long, ScenarioContext>();

        /// <summary>Создаёт пустое хранилище контекстов.</summary>
        public InMemoryScenarioContextRepository() { }

        /// <inheritdoc/>
        public Task<ScenarioContext?> GetContext(long userId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _scenarioContext.TryGetValue(userId, out var context);
            return Task.FromResult(context);
        }

        /// <inheritdoc/>
        public Task SetContext(long userId, ScenarioContext context, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _scenarioContext[userId] = context;
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task ResetContext(long userId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _scenarioContext.TryRemove(userId, out var value);
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<ScenarioContext>> GetContexts(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<ScenarioContext>>(_scenarioContext.Values.ToArray());
        }
    }
}
