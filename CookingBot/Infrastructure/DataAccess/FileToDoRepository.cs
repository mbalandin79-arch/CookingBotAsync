using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CookingBot.Core.DataAccess;
using CookingBot.Core.Entities;
using Telegram.Bot.Types;

namespace CookingBot.Infrastructure.DataAccess
{
    internal class FileToDoRepository : IToDoRepository
    {
        private readonly string _baseDirectory;
        private readonly string _indexPath;
        private Dictionary<Guid, Guid> _index = new Dictionary<Guid, Guid>();
        private bool _indexLoaded = false;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        public FileToDoRepository(string baseDirectory = "Todos")
        {
            _baseDirectory = baseDirectory;
            if (!Directory.Exists(_baseDirectory))
            {
                Directory.CreateDirectory(_baseDirectory);
            }
            _indexPath = Path.Combine(_baseDirectory, "index.json");
        }

        private async Task EnsureIndexLoadedAsync(CancellationToken ct)
        {
            if (_indexLoaded)
            {
                return;
            }

            if (File.Exists(_indexPath))
            {
                var json = await File.ReadAllTextAsync(_indexPath, ct);
                _index = JsonSerializer.Deserialize<Dictionary<Guid, Guid>>(json, _jsonOptions) ?? new Dictionary<Guid, Guid>();
            }
            else
            {
                await RebuildIndexAsync(ct);
            }

            _indexLoaded = true;
        }

        private async Task RebuildIndexAsync(CancellationToken ct)
        {
            _index = new Dictionary<Guid, Guid>();

            if (!Directory.Exists(_baseDirectory))
            {
                Directory.CreateDirectory(_baseDirectory);
                await SaveIndexAsync(ct);
                return;
            }

            foreach (var userDir in Directory.GetDirectories(_baseDirectory))
            {
                var dirName = Path.GetFileName(userDir);
                if (!Guid.TryParse(dirName, out Guid userId))
                    continue;

                foreach (var filePath in Directory.GetFiles(userDir, "*.json"))
                {
                    var fileName = Path.GetFileNameWithoutExtension(filePath);
                    if (Guid.TryParse(fileName, out Guid todoId))
                    {
                        _index[todoId] = userId;
                    }
                }
            }

            await SaveIndexAsync(ct);
        }

        private async Task SaveIndexAsync(CancellationToken ct)
        {
            var json = JsonSerializer.Serialize(_index, _jsonOptions);
            await File.WriteAllTextAsync(_indexPath, json, ct);
        }

        private string GetToDoFilePath(Guid userId, Guid todoId)
        {
            return Path.Combine(_baseDirectory, userId.ToString(), $"{todoId}.json");
        }

        private async Task<ToDoItem?> ReadToDoItemAsync(string filePath, CancellationToken ct)
        {
            if (!File.Exists(filePath))
                return null;

            var json = await File.ReadAllTextAsync(filePath, ct);
            return JsonSerializer.Deserialize<ToDoItem?>(json, _jsonOptions);
        }

        public async Task AddAsync(ToDoItem item, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(ct);
            try
            {
                await EnsureIndexLoadedAsync(ct);

                var userDir = Path.Combine(_baseDirectory, item.User.UserId.ToString());
                Directory.CreateDirectory(userDir);

                var filePath = GetToDoFilePath(item.User.UserId, item.Id);
                var json = JsonSerializer.Serialize(item, _jsonOptions);
                await File.WriteAllTextAsync(filePath, json, ct);

                _index[item.Id] = item.User.UserId;
                await SaveIndexAsync(ct);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<int> CountActiveAsync(Guid userId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(ct);
            try
            {
                await EnsureIndexLoadedAsync(ct);

                int count = 0;
                var todoIds = _index.Where(w => w.Value == userId).Select(s => s.Key).ToList();
                foreach (var todoId in todoIds)
                {
                    var filePath = GetToDoFilePath(userId, todoId);
                    var item = await ReadToDoItemAsync(filePath, ct);
                    if (item != null && item.State == ToDoItem.ToDoItemState.Active)
                        count++;
                }
                return count;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(ct);
            try
            {
                await EnsureIndexLoadedAsync(ct);

                if (!_index.TryGetValue(id, out Guid userId))
                    return;

                var filePath = GetToDoFilePath(userId, id);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                _index.Remove(id);
                await SaveIndexAsync(ct);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<bool> ExistsByNameAsync(Guid userId, string name, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(ct);
            try
            {
                await EnsureIndexLoadedAsync(ct);

                var todoIds = _index.Where(w => w.Value == userId).Select(s => s.Key).ToList();
                foreach (var todoId in todoIds)
                {
                    var filePath = GetToDoFilePath(userId, todoId);
                    var item = await ReadToDoItemAsync(filePath, ct);
                    if (item != null && string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                return false;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<IReadOnlyList<ToDoItem>> FindAsync(Guid userId, Func<ToDoItem, bool> predicate, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(ct);
            try
            {
                await EnsureIndexLoadedAsync(ct);

                var result = new List<ToDoItem>();
                var todoIds = _index.Where(w => w.Value == userId).Select(s => s.Key).ToList();
                foreach (var todoId in todoIds)
                {
                    var filePath = GetToDoFilePath(userId, todoId);
                    var item = await ReadToDoItemAsync(filePath, ct);
                    if (item != null && predicate(item))
                    {
                        result.Add(item);
                    }
                }
                return result;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<IReadOnlyList<ToDoItem>> GetActiveByUserIdAsync(Guid userId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(ct);
            try
            {
                await EnsureIndexLoadedAsync(ct);

                var result = new List<ToDoItem>();
                var todoIds = _index.Where(w => w.Value == userId).Select(s => s.Key).ToList();
                foreach (var todoId in todoIds)
                {
                    var filePath = GetToDoFilePath(userId, todoId);
                    var item = await ReadToDoItemAsync(filePath, ct);
                    if (item != null && item.State == ToDoItem.ToDoItemState.Active)
                    {
                        result.Add(item);
                    }
                }
                return result;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<IReadOnlyList<ToDoItem>> GetAllByUserIdAsync(Guid userId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(ct);
            try
            {
                await EnsureIndexLoadedAsync(ct);

                var result = new List<ToDoItem>();
                var todoIds = _index.Where(w => w.Value == userId).Select(s => s.Key).ToList();
                foreach (var todoId in todoIds)
                {
                    var filePath = GetToDoFilePath(userId, todoId);
                    var item = await ReadToDoItemAsync(filePath, ct);
                    if (item != null)
                    {
                        result.Add(item);
                    }
                }
                return result;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<ToDoItem?> GetAsync(Guid id, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(ct);
            try
            {
                await EnsureIndexLoadedAsync(ct);

                if (!_index.TryGetValue(id, out Guid userId))
                    return null;

                var filePath = GetToDoFilePath(userId, id);
                return await ReadToDoItemAsync(filePath, ct);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task UpdateAsync(ToDoItem item, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(ct);
            try
            {
                await EnsureIndexLoadedAsync(ct);

                if (!_index.TryGetValue(item.Id, out Guid userId))
                    return;

                var filePath = GetToDoFilePath(userId, item.Id);
                var json = JsonSerializer.Serialize(item, _jsonOptions);
                await File.WriteAllTextAsync(filePath, json, ct);
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
