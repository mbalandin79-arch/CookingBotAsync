using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Net;
using CookingBot.Core.Services;
using CookingBot.Infrastructure.DataAccess;
using CookingBot.TelegramBot;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace CookingBot
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                string settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                if (!File.Exists(settingsPath))
                {
                    Console.WriteLine($"Файл appsettings.json не найден по пути: {settingsPath}");
                    Console.WriteLine("Создайте файл и добавьте в него BotToken");
                    return;
                }

                string botToken;

                try
                {
                    var json = await File.ReadAllTextAsync(settingsPath, CancellationToken.None);
                    using var doc = JsonDocument.Parse(json);

                    if (!doc.RootElement.TryGetProperty("BotToken", out var tokenElement))
                    {
                        Console.WriteLine("В файле appsettings.json отсутствует свойство BotToken");
                        return;
                    }

                    botToken = tokenElement.GetString() ?? string.Empty;
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Ошибка парсинга appsettings.json: {ex.Message}");
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Произошла ошибка при чтении настроек: {ex.Message}");
                    return;
                }

                if (string.IsNullOrEmpty(botToken) || botToken == "Put_Your_Bot_Token_Here")
                {
                    Console.WriteLine("Токен бота не задан в appsettings.json");
                    Console.WriteLine("Получите токен у @BotFather и вставьте в файл");
                    return;
                }

                if (botToken.Length < 10)
                {
                    Console.WriteLine("Токен выглядит некорректным (слишком короткий). Проверьте appsettings.json");
                    return;
                }

                Console.WriteLine("Бот запускается...");
                Console.WriteLine($"Токен: (задан, {botToken.Length} символов)");
                Console.WriteLine();

                using var cts = new CancellationTokenSource();
                var userRepository = new FileUserRepository();
                var toDoRepository = new FileToDoRepository("Todos");
                var toDoService = new ToDoService(toDoRepository);
                var toDoReportService = new ToDoReportService(toDoService);
                var userService = new UserService(userRepository);
                var handler = new UpdateHandler(userService, toDoService, toDoReportService);
                var botClient = new TelegramBotClient(botToken);

                try
                {
                    var me = await botClient.GetMe(cts.Token);
                    Console.WriteLine($"Запущен бот @{me.Username}");
                }
                catch (Telegram.Bot.Exceptions.ApiRequestException ex) when (ex.ErrorCode == 401)
                {
                    Console.WriteLine("Неверный токен бота (Unauthorized). Проверьте appsettings.json.");
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при вызове GetMe: {ex.Message}");
                    return;
                }

                var receiverOptions = new ReceiverOptions()
                {
                    AllowedUpdates = new[]
                    {
                        UpdateType.Message,
                        UpdateType.CallbackQuery
                    },
                    DropPendingUpdates = true
                };
                botClient.StartReceiving(handler, receiverOptions, cts.Token);

                Console.WriteLine("Бот запущен. Нажмите ESC для остановки");
                while (Console.ReadKey(true).Key != ConsoleKey.Escape) ;
                cts.Cancel();
                Console.WriteLine("Остановка бота...");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Произошла непредвиденная ошибка: " + ex.Message);
            }
        }
    }
}
