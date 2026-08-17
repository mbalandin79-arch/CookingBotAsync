using System;
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
    internal class FileUserRepository : IUserRepository
    {
        private readonly string _folderPath;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        public FileUserRepository(string folderPath = "UserData")
        {
            _folderPath = folderPath;
            if (!Directory.Exists(_folderPath))
            {
                Directory.CreateDirectory(_folderPath);
            }
        }

        private string GetFilePath(Guid userId) => Path.Combine(_folderPath, $"{userId}.json");

        private async Task WriteToFileUserAsync(ToDoUser user, CancellationToken ct)
        {
            var filePath = GetFilePath(user.UserId);
            var json = JsonSerializer.Serialize(user, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json, ct);
        }

        public async Task AddAsync(ToDoUser user, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(ct);
            try
            {
                await WriteToFileUserAsync(user, ct);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task DeleteAsync(Guid userId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(ct);
            try
            {
                string filePath = GetFilePath(userId);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<ToDoUser?> GetUserByTelegramUserIdAsync(long telegramUserId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(ct);
            try
            {
                foreach (var file in Directory.GetFiles(_folderPath, "*.json"))
                {
                    var json = await File.ReadAllTextAsync(file, ct);
                    var user = JsonSerializer.Deserialize<ToDoUser>(json, _jsonOptions);
                    if (user != null && user.TelegramUserId == telegramUserId)
                    {
                        return user;
                    }
                }
                return null;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<ToDoUser?> GetUserByUserIdAsync(Guid userId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(ct);
            try
            {
                var filePath = GetFilePath(userId);
                if (!File.Exists(filePath))
                {
                    return null;
                }
                var json = await File.ReadAllTextAsync(filePath, ct);
                return JsonSerializer.Deserialize<ToDoUser>(json, _jsonOptions);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<IReadOnlyList<ToDoUser>> GetAllUsersAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(ct);
            try
            {
                var users = new List<ToDoUser>();
                foreach (var file in Directory.GetFiles(_folderPath, "*.json"))
                {
                    var json = await File.ReadAllTextAsync(file, ct);
                    var user = JsonSerializer.Deserialize<ToDoUser>(json, _jsonOptions);
                    if (user != null)
                    {
                        users.Add(user);
                    }
                }
                return users;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task UpdateAsync(ToDoUser user, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(ct);
            try
            {
                await WriteToFileUserAsync(user, ct);
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
