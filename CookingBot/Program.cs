using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Net;
using CookingBot.Core.Services;
using CookingBot.Core.Exceptions;
using CookingBot.Infrastructure.DataAccess;
using CookingBot.TelegramBot;
using CookingBot.TelegramBot.Scenarios;
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
                (string? botToken, string? errorMessage) = await GetToken(settingsPath);
                if (errorMessage != null)
                {
                    Console.WriteLine(errorMessage);
                    return;
                }                

                Console.WriteLine("Бот запускается...");
                Console.WriteLine($"Токен: (задан, {botToken!.Length} символов)");
                Console.WriteLine();

                using var cts = new CancellationTokenSource();
                var userRepository = new FileUserRepository();
                var toDoRepository = new FileToDoRepository("Todos");
                var toDoListRepository = new FileToDoListRepository("ToDoLists");
                var toDoService = new ToDoService(toDoRepository);
                var toDoReportService = new ToDoReportService(toDoService);
                var toDoListService = new ToDoListService(toDoListRepository);
                var userService = new UserService(userRepository);
                var contextRepository = new InMemoryScenarioContextRepository();
                var scenarios = new List<IScenario>
                {
                    new AddTaskScenario(userService, toDoService, toDoListService),
                    new AddListScenario(userService, toDoListService),
                    new DeleteListScenario(userService, toDoListService, toDoService)
                };
                var handler = new UpdateHandler(userService, toDoService, toDoReportService, contextRepository, scenarios, toDoListService);
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

        private static async Task<(string? botToken, string? errorMessage)> GetToken(string settingsPath)
        {
            if (!File.Exists(settingsPath))
            {
                return (null, $"Файл appsettings.json не найден по пути: {settingsPath}\nСоздайте файл и добавьте в него botToken");
            }

            try
            {
                var json = await File.ReadAllTextAsync(settingsPath, CancellationToken.None);
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("BotToken", out var tokenElement))
                    return (null, "В файле appsettings.json отсутствует свойство BotToken");

                string botToken = tokenElement.GetString() ?? string.Empty;

                if (string.IsNullOrEmpty(botToken) || botToken == "Put_Your_Bot_Token_Here")
                    return (null, "Токен бота не задан в appsettings.json\nПолучите токен у @BotFather и вставьте в файл");
                
                if (botToken.Length < 10)
                    return (null, "Токен выглядит некорректным (слишком короткий). Проверьте appsettings.json");

                return (botToken, null);
            }
            catch (JsonException ex)
            {
                return (null, $"Ошибка парсинга appsettings.json: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (null, $"Произошла непредвиденная ошибка: {ex.Message}");
            }
        }
    }
}
