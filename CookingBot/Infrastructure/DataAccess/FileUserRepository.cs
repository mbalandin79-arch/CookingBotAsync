using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using CookingBot.Core.DataAccess;
using CookingBot.Core.Entities;

using Otus.ToDoList.ConsoleBot.Types;

namespace CookingBot.Infrastructure.DataAccess
{
    internal class FileUserRepository : IUserRepository
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        private async Task<ToDoUser?> SearchUserInFileAsync(long telegramUserId, CancellationToken ct)
        {
            var allUsers = new List<ToDoUser>();
            var options = new JsonSerializerOptions { WriteIndented = true };
            var jsonfiles = Directory.GetFiles("*.json");

            foreach (var jsonfile in jsonfiles)
            {
                var json = await File.ReadAllTextAsync(jsonfile);
                var user = JsonSerializer.Deserialize<ToDoUser>(json, options);
                if (user != null) 
                { 
                    allUsers.Add(user); 
                }
            }

            return allUsers.FirstOrDefault(w => w.TelegramUserId == telegramUserId);
        }

        private async Task<ToDoUser?> ReadFromFileUserAsync(string filePath, CancellationToken ct)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<ToDoUser>(json, options) ?? null;
        }

        private async Task WriteToFileUserAsync(ToDoUser user, string filePath, CancellationToken ct)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(user, options);
            await File.WriteAllTextAsync(filePath, json);
        }

        public async Task AddAsync(ToDoUser user, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(ct);
            try
            {
                string filePath = user.UserId.ToString() + ".json";
                await WriteToFileUserAsync(user, filePath, ct);
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
                string filePath = userId.ToString() + ".json";
                File.Delete(filePath);
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
                return await SearchUserInFileAsync(telegramUserId, ct);
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
                string filePath = userId.ToString() + ".json";
                return await ReadFromFileUserAsync(filePath, ct);
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
                string filePath = user.UserId.ToString() + ".json";
                var exists = File.Exists(filePath);
                if (exists) 
                {
                    await WriteToFileUserAsync(user, filePath, ct);
                }                
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
