using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Net;
using System.Text.Json.Nodes;
using CookingBot.Core.Services;
using CookingBot.Core.Exceptions;
using CookingBot.Infrastructure.DataAccess;
using CookingBot.TelegramBot;
using CookingBot.TelegramBot.Scenarios;
using CookingBot.Helpers;
using CookingBot.BackgroundTasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace CookingBot
{
    /// <summary>
    /// Точка входа бота: конфигурация (appsettings.json), подключение к БД,
    /// сборка зависимостей (репозитории → сервисы → сценарии → UpdateHandler),
    /// запуск фоновых задач и приём обновлений Telegram. Остановка — ESC:
    /// сначала останавливаются фоновые задачи, затем отменяется токен.
    /// </summary>
    internal class Program
    {
        /// <summary>
        /// Основной метод: читает конфигурацию, проверяет токен и БД,
        /// создаёт все компоненты бота, запускает фоновые задачи и приём
        /// обновлений; ждёт ESC и корректно останавливает всё.
        /// </summary>
        /// <param name="args">Аргументы командной строки (не используются).</param>
        /// <returns>Задача (Task) основного метода.</returns>
        static async Task Main(string[] args)
        {
            try
            {
                using var cts = new CancellationTokenSource();
                string settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                await EnsureConfigurationAsync(settingsPath, cts.Token);
                (string? botToken, string? errorMessage) = await GetToken(settingsPath);
                if (errorMessage != null)
                {
                    Console.WriteLine(errorMessage);
                    WaitBeforeExit();
                    return;
                }

                Console.WriteLine("Бот запускается...");
                Console.WriteLine($"Токен: (задан, {botToken!.Length} символов)");
                Console.WriteLine();

                // Чтение лимитов и строки подключения из конфига (AppConfig — единственное место, знающее формат файла)
                var (maxTasksMember, maxTasksAdvanced, maxLengthTask, maxListsPerUserMember, maxListsPerUserAdvanced, maxRecipesPerList) = AppConfig.ReadLimits(settingsPath);

                // Настройки фоновых задач (целые часы; файл конфига правит только Администратор)
                var (resetScenarioRunIntervalHours, resetScenarioTimeoutHours) = AppConfig.ReadBackgroundTasks(settingsPath);
                if (resetScenarioRunIntervalHours <= 0 || resetScenarioTimeoutHours <= 0)
                {
                    Console.WriteLine("Настройки фоновых задач в appsettings.json должны быть целыми положительными числами (ResetScenarioRunIntervalHours, ResetScenarioTimeoutHours).");
                    WaitBeforeExit();
                    return;
                }

                // Строка подключения к PostgreSQL
                string? connectionString = AppConfig.ReadConnectionString(settingsPath);
                if (string.IsNullOrEmpty(connectionString) || connectionString == "Put_Your_Connection_String_Here") 
                {
                    Console.WriteLine("Строка подключения к базе данных не задана в appsettings.json (ConnectionString).");
                    WaitBeforeExit();
                    return;
                }

                // Фабрика подключений к БД: создаётся ОДИН раз при старте бота.
                // Репозитории получают её в конструктор и зовут CreateDataContext()
                // на каждый свой метод (подключение живёт недолго, внутри using)
                var dataContextFactory = new DataContextFactory(connectionString);

                // Ранняя проверка доступности БД: без неё бот запустился бы,
                // а ошибка выскочила бы только при первом обращении к репозиторию
                try
                {
                    using var probeContext = dataContextFactory.CreateDataContext();
                    _ = probeContext.ToDoUsers.Count();
                    Console.WriteLine("Подключение к базе данных: OK");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Не удалось подключиться к базе данных: " + ex.Message);
                    Console.WriteLine("Проверьте строку ConnectionString в appsettings.json (и запущен ли PostgreSQL).");
                    WaitBeforeExit();
                    return;
                }

                var userRepository = new SqlUserRepository(dataContextFactory);
                var toDoRepository = new SqlToDoRepository(dataContextFactory);
                var toDoListRepository = new SqlToDoListRepository(dataContextFactory);
                var toDoService = new ToDoService(toDoRepository);
                await toDoService.SetConfigurationAsync(maxTasksMember, maxTasksAdvanced, maxLengthTask, maxRecipesPerList, cts.Token);
                var toDoListService = new ToDoListService(toDoListRepository);
                await toDoListService.SetConfigurationAsync(maxListsPerUserMember, maxListsPerUserAdvanced, cts.Token);
                var toDoReportService = new ToDoReportService(toDoService, toDoListService);
                var userService = new UserService(userRepository);
                var notificationService = new NotificationService(dataContextFactory);
                var contextRepository = new InMemoryScenarioContextRepository();
                var scenarios = new List<IScenario>
                {
                    new AddTaskScenario(userService, toDoService, toDoListService),
                    new AddListScenario(userService, toDoListService),
                    new DeleteListScenario(userService, toDoListService),
                    new DeleteTaskScenario(toDoService)
                };
                var handler = new UpdateHandler(userService, toDoService, toDoReportService, contextRepository, scenarios, toDoListService, settingsPath);
                var botClient = new TelegramBotClient(botToken);

                // Ранняя проверка связи с Telegram и токена
                // (аналогично проверке БД выше):
                // GetMe выполняется до установки меню,
                // чтобы при отсутствии сети или неверном токене бота
                // печатал понятное сообщение и не запускался
                try
                {
                    var me = await botClient.GetMe(cts.Token);
                    Console.WriteLine($"Запущен бот @{me.Username}");
                }
                catch (Telegram.Bot.Exceptions.ApiRequestException ex) when (ex.ErrorCode == 401)
                {
                    Console.WriteLine("Неверный токен бота (Unauthorized). Проверьте appsettings.json.");
                    WaitBeforeExit();
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Нет соединения с Telegram: " + ex.Message);
                    Console.WriteLine("Проверьте подключение к Интернету (и токен в appsettings.json).");
                    WaitBeforeExit();
                    return;
                }

                var str = new StringBuilder();
                str.AppendLine("Конфигурация загружена:");
                str.AppendLine($"MaxTasksMember={maxTasksMember}");
                str.AppendLine($"MaxTasksAdvanced={maxTasksAdvanced}");
                str.AppendLine($"MaxLengthTask={maxLengthTask}");
                str.AppendLine($"MaxListsPerUserMember={maxListsPerUserMember}");
                str.AppendLine($"MaxListsPerUserAdvanced={maxListsPerUserAdvanced}");
                str.AppendLine($"MaxRecipesPerList={maxRecipesPerList}");
                Console.WriteLine(str.ToString());

                // Установка команд для меню BotCommand (базовый набор для всех)
                await botClient.SetMyCommands(Keyboards.GetCommandsForUser(null), cancellationToken: cts.Token);

                // Установка кнопок меню
                await botClient.SetChatMenuButton(menuButton: new MenuButtonCommands(), cancellationToken: cts.Token);

                var receiverOptions = new ReceiverOptions()
                {
                    AllowedUpdates = new[]
                    {
                        UpdateType.Message,
                        UpdateType.CallbackQuery
                    },
                    DropPendingUpdates = true
                };

                // Фоновые задачи: создаем раннер и регистрируем в нем задачи
                using var runner = new BackgroundTaskRunner();
                runner.AddTask(new ResetScenarioBackgroundTask(TimeSpan.FromHours(resetScenarioRunIntervalHours), 
                    TimeSpan.FromHours(resetScenarioTimeoutHours), contextRepository, userService, botClient));
                runner.AddTask(new NotificationBackgroundTask(notificationService, botClient));

                // Запуск фоновых задач - до запуска приема сообщений ботом
                // (StartTasks возвращает void: он только стартует задачи и сразу выходит)
                runner.StartTasks(cts.Token);

                botClient.StartReceiving(handler, receiverOptions, cts.Token);

                Console.WriteLine("Бот запущен. Нажмите ESC для остановки");
                while (Console.ReadKey(true).Key != ConsoleKey.Escape) ;

                // Остановка фоновых задач - до отмены CancellationTokenSource
                Console.WriteLine("Остановка фоновых задач...");
                await runner.StopTasks(cts.Token);

                cts.Cancel();
                Console.WriteLine("Остановка бота...");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Произошла непредвиденная ошибка: " + ex.Message);
            }

            WaitBeforeExit();
        }

        /// <summary>
        /// Пауза перед завершением программы: печатает подсказку и ждёт нажатия
        /// любой клавиши. Нужна, чтобы окно консоли не закрылось сразу при
        /// запуске exe двойным кликом — без паузы сообщения об ошибках печатаются
        /// и мгновенно исчезают вместе с окном (при запуске из VS сообщения и так
        /// остаются на экране, будет просто одно лишнее нажатие клавиши).
        /// </summary>
        private static void WaitBeforeExit()
        {
            Console.WriteLine("Нажмите любую клавишу для выхода...");
            Console.ReadKey(true);
        }

        /// <summary>
        /// Читает и валидирует токен бота из appsettings.json:
        /// файл существует, свойство BotToken есть, не заглушка, не слишком короткий.
        /// </summary>
        /// <param name="settingsPath">Полный путь к appsettings.json.</param>
        /// <returns>Кортеж: токен (при успехе) или сообщение об ошибке (при неудаче).</returns>
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

        /// <summary>
        /// Строгая проверка appsettings.json: набор ключей файла должен
        /// ТОЧНО совпадать с шаблоном (ни лишних, ни недостающих ключей).
        /// Если файл отсутствует, повреждён или не соответствует шаблону —
        /// он ПОЛНОСТЬЮ пересоздаётся по шаблону, БЕЗ переноса данных
        /// (в том числе BotToken и ConnectionString станут заглушками -
        /// их придётся вписать заново).
        /// </summary>
        /// <param name="settingsPath">Полный путь к appsettings.json.</param>
        /// <param name="ct">Токен отмены операции.</param>
        private static async Task EnsureConfigurationAsync(string settingsPath, CancellationToken ct)
        {
            // Эталонный набор ключей (единственные ключи, которые читает код)
            var expectedKeys = new[]
            {
                "BotToken", "ConnectionString",
                "MaxTasksMember", "MaxTasksAdvanced", "MaxLengthTask",
                "MaxListsPerUserMember", "MaxListsPerUserAdvanced", "MaxRecipesPerList",
                "ResetScenarioRunIntervalHours", "ResetScenarioTimeoutHours"
            };

            // Файл считается годным, только если он существует, читается
            // и содержит ровно эталонный набор ключей
            bool isValid = false;
            if (File.Exists(settingsPath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(settingsPath, ct);
                    var root = JsonNode.Parse(json)?.AsObject();
                    isValid = root != null &&
                        root.Count == expectedKeys.Length &&
                        expectedKeys.All(key => root.ContainsKey(key));
                }
                catch (JsonException)
                {
                    // Файл повреждён (не разбирается как JSON) - пересоздадим
                    isValid = false;
                }
            }

            if (isValid)
                return;

            // Шаблон конфигурации: заглушки секретов + значения по умолчанию
            var template = new JsonObject
            {
                ["BotToken"] = "Put_Your_Bot_Token_Here",
                ["ConnectionString"] = "Put_Your_Connection_String_Here",
                ["MaxTasksMember"] = 100,
                ["MaxTasksAdvanced"] = 200,
                ["MaxLengthTask"] = 100,
                ["MaxListsPerUserMember"] = 10,
                ["MaxListsPerUserAdvanced"] = 20,
                ["MaxRecipesPerList"] = 50,
                ["ResetScenarioRunIntervalHours"] = 1,
                ["ResetScenarioTimeoutHours"] = 1
            };
            await File.WriteAllTextAsync(settingsPath, template.ToString(), ct);
            Console.WriteLine("appsettings.json отсутствовал, повреждён или не соответствовал шаблону - файл создан заново по шаблону.");
            Console.WriteLine("ВНИМАНИЕ: данные не перенесены. Впишите заново BotToken и ConnectionString.");
        }
    }
}
