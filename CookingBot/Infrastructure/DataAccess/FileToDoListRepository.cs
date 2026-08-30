using CookingBot.Core.DataAccess;
using CookingBot.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading.Tasks;

namespace CookingBot.Infrastructure.DataAccess
{
    public class FileToDoListRepository : IToDoListRepository
    {
        private readonly string _baseDirectory;
        private readonly string _indexPath;
        private Dictionary<Guid, Guid> _index = new Dictionary<Guid, Guid>();
        private bool _indexLoaded = false;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public FileToDoListRepository(string baseDirectory = "ToDoLists")
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

        private string GetToDoFilePath(Guid userId, Guid todoListId)
        {
            return Path.Combine(_baseDirectory, userId.ToString(), $"{todoListId}.json");
        }

        private async Task<ToDoList?> ReadToDoListAsync(string filePath, CancellationToken ct)
        {
            if (!File.Exists(filePath))
                return null;

            var json = await File.ReadAllTextAsync(filePath, ct);
            return JsonSerializer.Deserialize<ToDoList?>(json, _jsonOptions);
        }
        
        public async Task AddAsync(ToDoList todoList, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(ct);
            try
            {
                await EnsureIndexLoadedAsync(ct);

                var userDir = Path.Combine(_baseDirectory, todoList.User.UserId.ToString());
                Directory.CreateDirectory(userDir);

                var filePath = GetToDoFilePath(todoList.User.UserId, todoList.Id);
                var json = JsonSerializer.Serialize(todoList, _jsonOptions);
                await File.WriteAllTextAsync(filePath, json, ct);

                _index[todoList.Id] = todoList.User.UserId;
                await SaveIndexAsync(ct);
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

                var todoListIds = _index.Where(w => w.Value == userId).Select(s => s.Key).ToList();
                foreach (var todoListId in todoListIds)
                {
                    var filePath = GetToDoFilePath(userId, todoListId);
                    var list = await ReadToDoListAsync(filePath, ct);
                    if (list != null && string.Equals(list.Name, name, StringComparison.OrdinalIgnoreCase))
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

        public async Task<ToDoList?> GetAsync(Guid id, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(ct);
            try
            {
                await EnsureIndexLoadedAsync(ct);

                if (!_index.TryGetValue(id, out Guid userId))
                    return null;

                var filePath = GetToDoFilePath(userId, id);
                return await ReadToDoListAsync(filePath, ct);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<IReadOnlyList<ToDoList>> GetByUserIdAsync(Guid userId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(ct);
            try
            {
                await EnsureIndexLoadedAsync(ct);

                var result = new List<ToDoList>();
                var todoListIds = _index.Where(w => w.Value == userId).Select(s => s.Key).ToList();
                foreach (var todoListId in todoListIds)
                {
                    var filePath = GetToDoFilePath(userId, todoListId);
                    var list = await ReadToDoListAsync(filePath, ct);
                    if (list != null)
                    {
                        result.Add(list);
                    }
                }
                return result;
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
