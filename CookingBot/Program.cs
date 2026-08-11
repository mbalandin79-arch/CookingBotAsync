using System.Diagnostics;
using System.Reflection;
using System.Text;
using CookingBot.Core.Services;
using CookingBot.Infrastructure.DataAccess;
using CookingBot.TelegramBot;
using Otus.ToDoList.ConsoleBot;

namespace CookingBot
{
    internal class Program
    {
        static void Main(string[] args)
        {
            try
            {
                using var cts = new CancellationTokenSource();
                FileUserRepository userRepository = new FileUserRepository();
                FileToDoRepository toDoRepository = new FileToDoRepository("Todos");
                //InMemoryUserRepository userRepository = new InMemoryUserRepository();
                //InMemoryToDoRepository toDoRepository = new InMemoryToDoRepository();                
                ToDoService toDoService = new ToDoService(toDoRepository);
                ToDoReportService toDoReportService = new ToDoReportService(toDoService);
                UserService userService = new UserService(userRepository);
                UpdateHandler handler = new UpdateHandler(userService, toDoService, toDoReportService);
                ConsoleBotClient botClient = new ConsoleBotClient();
                botClient.StartReceiving(handler, cts.Token);
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
