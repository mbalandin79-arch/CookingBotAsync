using System;
using System.Threading.Tasks;
using CookingBot.Core.Entities;
using CookingBot.Helpers;
using CookingBot.TelegramBot.Dto;
using CookingBot.TelegramBot.Scenarios;
using static CookingBot.TelegramBot.Scenarios.ScenarioContext;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace CookingBot.TelegramBot
{
    internal partial class UpdateHandler
    {
        /// <summary>
        /// Точка входа всех callback-запросов (нажатий inline-кнопок):
        /// при активном сценарии передаёт нажатие в сценарий; «deletetask|»
        /// запускает сценарий удаления рецепта (с проверкой владельца);
        /// «showtask|» → HandleTaskCallbackAsync; остальное →
        /// HandleCallbackSwitchAsync.
        /// </summary>
        /// <param name="telegramBotClient">Клиент Telegram.</param>
        /// <param name="callbackQuery">Callback-запрос (нажатая кнопка).</param>
        /// <param name="ct">Токен отмены операции.</param>
        private async Task HandleCallbackQueryAsync(ITelegramBotClient telegramBotClient,
            CallbackQuery callbackQuery, CancellationToken ct)
        {
            // защита от пустого callbackQuery.Data
            var data = callbackQuery.Data;
            if (string.IsNullOrEmpty(data))
                return;

            // отвечаем, что нажатие принято
            await telegramBotClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: ct);

            // извлекаем нажатие
            if (callbackQuery.Message == null)
                return;
            var chat = callbackQuery.Message.Chat;
            long userId = callbackQuery.From.Id;

            // проверка активного сценария - callback передается в сценарий
            var scenarioContext = await _contextRepository.GetContext(userId, ct);
            if (scenarioContext != null)
            {
                var callbackUpdate = new Update { CallbackQuery = callbackQuery };
                await ProcessScenarioAsync(telegramBotClient, callbackUpdate, scenarioContext, userId, ct);
                return;
            }

            // запуск DeleteTaskScenario при нажатии "❌Удалить"
            if (data.StartsWith("deletetask|"))
            {
                var dto = ToDoItemCallbackDto.FromString(data);
                var task = await _todoService.GetTaskAsync(dto.ToDoItemId, ct);
                var currentUser = await _userService.GetUserAsync(userId, ct);
                if (task == null || currentUser == null || task.User.TelegramUserId != currentUser.TelegramUserId)
                {
                    await telegramBotClient.SendMessage(chat, "Вы можете удалять только свои рецепты", cancellationToken: ct);
                    return;
                }
                var ctx = new ScenarioContext(ScenarioType.DeleteTask);
                ctx.UserId = userId;
                ctx.Data["taskId"] = dto.ToDoItemId;
                var startUpdate = new Update { Message = callbackQuery.Message };
                await ProcessScenarioAsync(telegramBotClient, startUpdate, ctx, userId, ct);
                return;
            }

            if (data.StartsWith("showtask|"))
            {
                await HandleTaskCallbackAsync(telegramBotClient, data, chat, userId, ct);
                return;
            }

            await HandleCallbackSwitchAsync(telegramBotClient, data, chat, userId, callbackQuery, ct);
        }

        /// <summary>
        /// Маршрутизатор простых действий кнопок (switch по callback-данным):
        /// запуск сценариев (addlist, deletelist, add_recipe), регистрация,
        /// навигация по меню (mainmenu, cook_menu, profile_menu, admin_menu),
        /// поиск (find*_recipe), показ рецептов, админ-действия (→ Admin),
        /// нераспознанное — в HandlePrefixedCallbackAsync.
        /// </summary>
        /// <param name="telegramBotClient">Клиент Telegram.</param>
        /// <param name="data">Callback-данные кнопки.</param>
        /// <param name="chat">Чат с пользователем.</param>
        /// <param name="userId">Идентификатор пользователя в Telegram.</param>
        /// <param name="callbackQuery">Callback-запрос (для запуска сценариев).</param>
        /// <param name="ct">Токен отмены операции.</param>
        private async Task HandleCallbackSwitchAsync(ITelegramBotClient telegramBotClient,
            string data, Chat chat, long userId, CallbackQuery callbackQuery, CancellationToken ct)
        {
            switch (data)
            {
                case "addlist":
                    {
                        var prompt = "Создание списка. Для отмены нажмите \"Отмена\".";
                        await StartScenarioAsync(telegramBotClient, ScenarioType.AddList, prompt, chat,
                            userId, callbackQuery, ct);
                        break;
                    }
                case "deletelist":
                    {
                        var prompt = "Удаление списка. Для отмены нажмите \"Отмена\".";
                        await StartScenarioAsync(telegramBotClient, ScenarioType.DeleteList, prompt, chat,
                            userId, callbackQuery, ct);
                        break;
                    }
                case "reg_yes":
                case "reg_no":
                case "reg_default":
                    await HandleRegistrationCallbackAsync(telegramBotClient, callbackQuery, data, chat,
                        userId, ct);
                    break;
                case "mainmenu":
                    SetState(userId, HandlerState.Ready);
                    await SendMainMenuAsync(telegramBotClient, chat, userId, ct);
                    break;
                case "cook_menu":
                    await SendCookMenuAsync(telegramBotClient, chat, userId, ct);
                    break;
                case "profile_menu":
                    await SendProfileMenuAsync(telegramBotClient, chat, userId, ct);
                    break;
                case "admin_menu":
                    await SendAdminMenuAsync(telegramBotClient, chat, userId, ct);
                    break;
                case "add_recipe":
                    {
                        var prompt = "Добавление рецепта. Для отмены нажмите \"Отмена\".";
                        await StartScenarioAsync(telegramBotClient, ScenarioType.AddTask, prompt, chat,
                            userId, callbackQuery, ct);
                        break;
                    }
                case "find_recipe":
                case "findall_recipe":
                    SetState(userId, HandlerState.AwaitingFindAllName);
                    await telegramBotClient.SendMessage(chat, "Введите имя для поиска:",
                        replyMarkup: Keyboards.BuildCancelKeyboard(), cancellationToken: ct);
                    break;
                case "findmy_recipe":
                    SetState(userId, HandlerState.AwaitingFindName);
                    await telegramBotClient.SendMessage(chat, "Введите имя для поиска:",
                        replyMarkup: Keyboards.BuildCancelKeyboard(), cancellationToken: ct);
                    break;
                case "show_recipes":
                case "showall_recipes":
                    await ShowAllRecipesAsync(telegramBotClient, chat, ct);
                    break;
                case "showmy_recipes":
                    await ShowListsAsync(telegramBotClient, chat, userId, ct);
                    break;
                case "mod_listusers":
                case "admin_promote_mod":
                case "admin_promote_admin":
                case "admin_demote_advanced":
                case "admin_demote_mod":
                case "admin_limits":
                case "admin_cleanup_locks":
                case "config_MaxTasksMember":
                case "config_MaxTasksAdvanced":
                case "config_MaxLengthTask":
                case "config_MaxListsPerUserMember":
                case "config_MaxListsPerUserAdvanced":
                case "config_MaxRecipesPerList":
                    await HandleAdminCallbackAsync(telegramBotClient, data, chat, userId, ct);
                    break;
                default:
                    await HandlePrefixedCallbackAsync(telegramBotClient, data, chat, userId,
                        callbackQuery.Message!.MessageId, ct);
                    break;
            }
        }

        /// <summary>
        /// Запускает сценарий с кнопки: создаёт контекст, показывает подсказку
        /// с клавиатурой «Отмена» и передаёт первое «обновление» в сценарий.
        /// </summary>
        /// <param name="telegramBotClient">Клиент Telegram.</param>
        /// <param name="type">Тип запускаемого сценария.</param>
        /// <param name="prompt">Подсказка пользователю перед стартом.</param>
        /// <param name="chat">Чат с пользователем.</param>
        /// <param name="userId">Идентификатор пользователя в Telegram.</param>
        /// <param name="callbackQuery">Нажатая кнопка (из её сообщения строится стартовое обновление).</param>
        /// <param name="ct">Токен отмены операции.</param>
        private async Task StartScenarioAsync(ITelegramBotClient telegramBotClient,
            ScenarioType type, string prompt, Chat chat, long userId, CallbackQuery callbackQuery,
            CancellationToken ct)
        {
            var ctx = new ScenarioContext(type);
            ctx.UserId = userId;
            await telegramBotClient.SendMessage(chat, prompt, replyMarkup: Keyboards.BuildCancelKeyboard(),
                cancellationToken: ct);
            var startUpdate = new Update { Message = callbackQuery.Message };
            await ProcessScenarioAsync(telegramBotClient, startUpdate, ctx, userId, ct);
        }

        /// <summary>
        /// Обрабатывает кнопки регистрации: «Да» — запрос имени
        /// (AwaitingRegistrationName); «Нет» — отмена (AwaitingStart);
        /// «Оставить по умолчанию» — регистрация с именем из Telegram.
        /// </summary>
        /// <param name="telegramBotClient">Клиент Telegram.</param>
        /// <param name="callbackQuery">Callback-запрос (источник имени из Telegram).</param>
        /// <param name="data">Callback-данные кнопки (reg_yes/reg_no/reg_default).</param>
        /// <param name="chat">Чат с пользователем.</param>
        /// <param name="userId">Идентификатор пользователя в Telegram.</param>
        /// <param name="ct">Токен отмены операции.</param>
        private async Task HandleRegistrationCallbackAsync(ITelegramBotClient telegramBotClient,
            CallbackQuery callbackQuery, string data, Chat chat, long userId, CancellationToken ct)
        {
            switch (data)
            {
                case "reg_yes":
                    {
                        var displayName = callbackQuery.From?.Username ?? $"User_{userId}";
                        var prompt = $" Ваше отображаемое Имя \"{displayName}\" ";
                        await telegramBotClient.SendMessage(chat, prompt, cancellationToken: ct);
                        prompt = " Если хотите изменить, введите новое Имя. Если нет, нажмите \"Оставить по умолчанию\"";
                        await telegramBotClient.SendMessage(chat, prompt,
                            replyMarkup: Keyboards.BuildRegistrationNameKeyboard(), cancellationToken: ct);
                        SetState(userId, HandlerState.AwaitingRegistrationName);
                        break;
                    }
                case "reg_no":
                    {
                        var prompt = " Регистрация отменена. Для начала работы выберите \"Старт\"";
                        await telegramBotClient.SendMessage(chat, prompt, cancellationToken: ct);
                        SetState(userId, HandlerState.AwaitingStart);
                        break;
                    }
                case "reg_default":
                    {
                        await UserRegistrationAsync(telegramBotClient, chat, callbackQuery.From?.Username,
                            string.Empty, userId, ct);
                        SetState(userId, HandlerState.Ready);
                        break;
                    }
            }
        }

        /// <summary>
        /// Обрабатывает команды с параметрами: pipe-разделённые (show|, showall|) 
        /// и underscore-префиксы (SetState, ChangeName, DeleteAccount, ConfirmDelete, Profile,
        /// ShowReport — по enum PrefixedCommand).
        /// </summary>
        /// <param name="telegramBotClient">Клиент Telegram.</param>
        /// <param name="data">Callback-данные кнопки.</param>
        /// <param name="chat">Чат с пользователем.</param>
        /// <param name="userId">Идентификатор пользователя в Telegram.</param>
        /// <param name="messageId">Id сообщения для редактирования (постраничный показ).</param>
        /// <param name="ct">Токен отмены операции.</param>
        private async Task HandlePrefixedCallbackAsync(ITelegramBotClient telegramBotClient,
            string data, Chat chat, long userId, int messageId, CancellationToken ct)
        {
            // pipe-разделённые команды (через DTO)
            if (data.StartsWith("show|"))
            {
                await HandleShowListAsync(telegramBotClient, data, chat, userId, messageId, ct);
                return;
            }
            if (data.StartsWith("showall|"))
            {
                await HandleShowAllAsync(telegramBotClient, data, chat, messageId, ct);
                return;
            }

            // underscore-префиксы через enum
            var command = GetPrefixedCommand(data);
            switch (command)
            {
                case PrefixedCommand.SetState:
                    await HandleSetStateCallbackAsync(telegramBotClient, chat, data, userId, ct);
                    break;
                case PrefixedCommand.ChangeName:
                    if (TryGetTargetUserId(data, out Guid changeNameUserId))
                    {
                        SetState(userId, HandlerState.AwaitingChangeName);
                        await telegramBotClient.SendMessage(chat, "Введите новое имя",
                            replyMarkup: Keyboards.BuildCancelKeyboard(), cancellationToken: ct);
                        _changeNameTargets[userId] = changeNameUserId;
                    }
                    break;
                case PrefixedCommand.DeleteAccount:
                    if (TryGetTargetUserId(data, out Guid deleteUserId))
                    {
                        var prompt = "Вы уверены? Все ваши данные будут удалены.";
                        await telegramBotClient.SendMessage(chat, prompt, replyMarkup:
                            Keyboards.BuildDeleteAccountKeyboard(deleteUserId), cancellationToken: ct);
                    }
                    break;
                case PrefixedCommand.ConfirmDelete:
                    if (TryGetTargetUserId(data, out Guid confirmUserId))
                    {
                        var selfUser = await _userService.GetUserAsync(userId, ct);
                        if (selfUser == null || selfUser.UserId != confirmUserId)
                        {
                            var prompt = "Удаление доступно только для своего аккаунта.";
                            await telegramBotClient.SendMessage(chat, prompt, cancellationToken: ct);
                            break;
                        }

                        try
                        {
                            // Рецепты и списки пользователя удаляет сама БД:
                            // внешние ключи FK_ToDoItem_ToDoUser и FK_ToDoList_ToDoUser
                            // настроены на ON DELETE CASCADE
                            await _userService.DeleteUserByUserIdAsync(confirmUserId, ct);

                            var prompt = "Аккаунт и все ваши данные удалены.";
                            await telegramBotClient.SendMessage(chat, prompt, cancellationToken: ct);

                            // Возврат гостевого меню ☰: пользователь больше не зарегистрирован.
                            await telegramBotClient.SetMyCommands(Keyboards.GetCommandsForUser(null),
                                scope: new BotCommandScopeChat { ChatId = userId }, cancellationToken: ct);
                        }
                        catch (Exception ex)
                        {
                            FileLogger.LogError(ex, "ConfirmDelete");
                            var prompt = "Не удалось удалить аккаунт. Подробности записаны в лог.";
                            await telegramBotClient.SendMessage(chat, prompt, cancellationToken: ct);
                        }
                    }
                    break;
                case PrefixedCommand.Profile:
                    await ShowProfileInfoAsync(telegramBotClient, chat, userId, ct);
                    break;
                case PrefixedCommand.ShowReport:
                    await ReportAsync(telegramBotClient, chat, userId, ct);
                    break;
                case PrefixedCommand.Unknown:
                    await telegramBotClient.SendMessage(chat, "Неизвестная команда.", cancellationToken: ct);
                    break;
            }
        }

        /// <summary>Подбирает сценарий по типу (первый, кто ответил CanHandle).</summary>
        /// <param name="type">Тип сценария.</param>
        /// <returns>Сценарий; null, если подходящего нет.</returns>
        private IScenario? GetScenario(ScenarioType type)
        {
            return _scenarios.FirstOrDefault(s => s.CanHandle(type));
        }

        /// <summary>
        /// Передаёт сообщение/нажатие в активный сценарий и обрабатывает результат:
        /// Completed — сброс контекста, «✅» и главное меню; Transition —
        /// сохранение контекста. Сценарий не найден или упал с исключением —
        /// сброс контекста, сообщение об ошибке и главное меню.
        /// </summary>
        /// <param name="telegramBotClient">Клиент Telegram.</param>
        /// <param name="update">Обновление (сообщение или callback).</param>
        /// <param name="context">Контекст сценария пользователя.</param>
        /// <param name="userId">Идентификатор пользователя в Telegram.</param>
        /// <param name="ct">Токен отмены операции.</param>
        private async Task ProcessScenarioAsync(ITelegramBotClient telegramBotClient, Update update,
            ScenarioContext context, long userId, CancellationToken ct)
        {
            var scenario = GetScenario(context.CurrentScenario);
            if (scenario == null)
            {
                await _contextRepository.ResetContext(userId, ct);
                var chat = update.Message?.Chat ?? update.CallbackQuery?.Message?.Chat;
                if (chat != null)
                {
                    await telegramBotClient.SendMessage(chat, "Сценарий не найден.",
                        replyMarkup: new ReplyKeyboardRemove(), cancellationToken: ct);
                    await SendMainMenuAsync(telegramBotClient, chat, userId, ct);
                }
                return;
            }

            ScenarioResult result;
            try
            {
                result = await scenario.HandleMessageAsync(telegramBotClient, context, update, ct);
            }
            catch (Exception ex)
            {
                FileLogger.LogError(ex, $"ProcessScenarioAsync user: {userId}, scenario: {context.CurrentScenario}");
                await _contextRepository.ResetContext(userId, ct);
                var chat = update.Message?.Chat ?? update.CallbackQuery?.Message?.Chat;
                if (chat != null)
                {
                    await telegramBotClient.SendMessage(chat, $"Ошибка при выполнении сценария: {ex.Message}",
                        replyMarkup: new ReplyKeyboardRemove(), cancellationToken: ct);
                    await SendMainMenuAsync(telegramBotClient, chat, userId, ct);
                }
                return;
            }

            if (result == ScenarioResult.Completed)
            {
                await _contextRepository.ResetContext(userId, ct);
                var chat = update.Message?.Chat ?? update.CallbackQuery?.Message?.Chat;
                if (chat != null)
                {
                    await telegramBotClient.SendMessage(chat, "✅", replyMarkup: new ReplyKeyboardRemove(),
                        cancellationToken: ct);
                    await SendMainMenuAsync(telegramBotClient, chat, userId, ct);
                }
            }
            else
            {
                await _contextRepository.SetContext(userId, context, ct);
            }
        }
    }
}
