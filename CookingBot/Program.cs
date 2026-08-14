using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
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
        static void Main(string[] args)
        {
            try
            {
                string settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                string json = File.ReadAllText(settingsPath);
                using JsonDocument doc = JsonDocument.Parse(json);
                string botToken = doc.RootElement.GetProperty("BotToken").GetString() ?? string.Empty;

                if (string.IsNullOrEmpty(botToken) || botToken == "Put_Your_Bot_Token_Here")
                {
                    Console.WriteLine(" Токен бота не задан в appsettings.json");
                    Console.WriteLine(" Получите токен у @BotFather и вставьте в файл");
                    return;
                }

                Console.WriteLine(" Бот запускается...");
                Console.WriteLine($" Токен: (задан, {botToken.Length} символов)");
                Console.WriteLine();

                using var cts = new CancellationTokenSource();
                FileUserRepository userRepository = new FileUserRepository();
                FileToDoRepository toDoRepository = new FileToDoRepository("Todos");             
                ToDoService toDoService = new ToDoService(toDoRepository);
                ToDoReportService toDoReportService = new ToDoReportService(toDoService);
                UserService userService = new UserService(userRepository);
                UpdateHandler handler = new UpdateHandler(userService, toDoService, toDoReportService);
                var botClient = new TelegramBotClient(botToken);
                var receiverOptions = new ReceiverOptions()
                {
                    AllowedUpdates = new[] { UpdateType.Message },
                    DropPendingUpdates = true
                };
                botClient.StartReceiving(handler, receiverOptions, cts.Token);

                Console.WriteLine(" Бот запущен. Нажмите Enter для остановки");
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine(" Произошла непредвиденная ошибка:");
                Console.WriteLine($" Тип ошибки: {ex.GetType().Name}");
                Console.WriteLine($" Сообщение: {ex.Message}");
                var trace = new StackTrace(ex, true);
                foreach (var item in trace.GetFrames())
                {
                    Console.WriteLine($"Файл: {item.GetFileName()}, Строка: {item.GetFileLineNumber()}, Метод: {item.GetMethod()}");
                }
                if (ex.InnerException != null)
                {
                    Console.WriteLine(" Внутреннее исключение:");
                    Console.WriteLine($" Тип: {ex.InnerException.GetType().Name}");
                    Console.WriteLine($" Сообщение: {ex.InnerException.Message}");
                    Console.WriteLine($" ");
                    var newtrace = new StackTrace(ex.InnerException, true);
                    foreach (var item in newtrace.GetFrames())
                    {
                        Console.WriteLine($"Файл: {item.GetFileName()}, Строка: {item.GetFileLineNumber()}, Метод: {item.GetMethod()}");
                    }
                }
            }
        }
    }
}
