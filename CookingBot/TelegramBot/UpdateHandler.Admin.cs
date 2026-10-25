using System;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using CookingBot.Core.Entities;
using CookingBot.Helpers;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace CookingBot.TelegramBot
{
    internal partial class UpdateHandler
    {
        /// <summary>
        /// Отправляет меню администрирования (клавиатура по роли);
        /// Moderator/Admin — меню, остальным — «нет прав».
        /// </summary>
        /// <param name="telegramBotClient">Клиент Telegram.</param>
        /// <param name="chat">Чат с пользователем.</param>
        /// <param name="userId">Идентификатор пользователя в Telegram.</param>
        /// <param name="ct">Токен отмены операции.</param>
        private async Task SendAdminMenuAsync(ITelegramBotClient telegramBotClient, Chat chat,
            long userId, CancellationToken ct)
        {
            var user = await _userService.GetUserAsync(userId, ct);
            if (user != null &&
                (user.State == ToDoUser.ToDoUserState.Admin ||
                user.State == ToDoUser.ToDoUserState.Moderator))
            {
                await telegramBotClient.SendMessage(chat, "Администрирование:",
                    replyMarkup: Keyboards.BuildAdminMenuKeyboard(user.State), cancellationToken: ct);
            }
            else
            {
                var prompt = "У вас нет прав доступа к этому разделу.";
                await telegramBotClient.SendMessage(chat, prompt, cancellationToken: ct);
            }
        }

        /// <summary>
        /// Маршрутизатор кнопок администрирования: список пользователей,
        /// повышение/понижение ролей (показ списка пользователей для выбора),
        /// лимиты, очистка журнала. Часть действий — только для Admin
        /// (повышение до Moderator/Admin, лимиты).
        /// </summary>
        /// <param name="telegramBotClient">Клиент Telegram.</param>
        /// <param name="data">Callback-данные кнопки.</param>
        /// <param name="chat">Чат с пользователем.</param>
        /// <param name="userId">Идентификатор пользователя в Telegram.</param>
        /// <param name="ct">Токен отмены операции.</param>
        private async Task HandleAdminCallbackAsync(ITelegramBotClient telegramBotClient,
            string data, Chat chat, long userId, CancellationToken ct)
        {
            var user = await _userService.GetUserAsync(userId, ct);
            if (user == null || (user.State != ToDoUser.ToDoUserState.Moderator &&
                user.State != ToDoUser.ToDoUserState.Admin))
            {
                var prompt = "У вас нет прав доступа к этому разделу.";
                await telegramBotClient.SendMessage(chat, prompt, cancellationToken: ct);
                return;
            }

            var adminOnly = data is "admin_promote_mod" or "admin_promote_admin" or
                "admin_demote_advanced" or "admin_demote_mod" or "admin_limits" or
                "config_MaxTasks" or "config_MaxLengthTask" or "config_MaxListsPerUser" or
                "config_MaxRecipesPerList";

            if (adminOnly && user.State != ToDoUser.ToDoUserState.Admin)
            {
                var prompt = "Требуются права Администратора.";
                await telegramBotClient.SendMessage(chat, prompt, cancellationToken: ct);
                return;
            }

            switch (data)
            {
                case "mod_listusers":
                    await ListUsersAsync(telegramBotClient, chat, ct);
                    await SendMainMenuAsync(telegramBotClient, chat, userId, ct);
                    break;
                case "mod_promote_member":
                    {
                        var prompt = "Выберите пользователя для повышения до Member:";
                        await ShowUserListForStateChangeAsync(telegramBotClient, chat, "setstate_member", prompt, ct);
                        break;
                    }
                case "mod_demote_guest":
                    {
                        var prompt = "Выберите пользователя для понижения до Guest:";
                        await ShowUserListForStateChangeAsync(telegramBotClient, chat, "setstate_guest", prompt, ct);
                        break;
                    }
                case "admin_promote_mod":
                    {
                        var prompt = "Выберите пользователя для повышения до Moderator:";
                        await ShowUserListForStateChangeAsync(telegramBotClient, chat, "setstate_moderator", prompt, ct);
                        break;
                    }
                case "admin_promote_admin":
                    {
                        var prompt = "Выберите пользователя для повышения до Admin:";
                        await ShowUserListForStateChangeAsync(telegramBotClient, chat, "setstate_admin", prompt, ct);
                        break;
                    }
                case "admin_demote_advanced":
                    {
                        var prompt = "Выберите пользователя для понижения до Advanced:";
                        await ShowUserListForStateChangeAsync(telegramBotClient, chat, "setstate_advanced", prompt, ct);
                        break;
                    }
                case "admin_demote_mod":
                    {
                        var prompt = "Выберите пользователя для понижения до Moderator:";
                        await ShowUserListForStateChangeAsync(telegramBotClient, chat, "setstate_moderator", prompt, ct);
                        break;
                    }
                case "admin_limits":
                    await ShowLimitsAsync(telegramBotClient, chat, ct);
                    break;
                case "admin_cleanup_locks":
                    await CleanupGuestLocksAsync(telegramBotClient, chat, ct);
                    break;
                case "config_MaxTasks":
                case "config_MaxLengthTask":
                case "config_MaxListsPerUser":
                case "config_MaxRecipesPerList":
                    {
                        _configLimitTargets[userId] = data.Substring("config_".Length);
                        SetState(userId, HandlerState.AwaitingConfigLimit);
                        var prompt = $"Введите новое значение для {_configLimitTargets[userId]} (1-1000):";
                        await telegramBotClient.SendMessage(chat, prompt,
                            replyMarkup: Keyboards.BuildCancelKeyboard("admin_limits"), cancellationToken: ct);
                        break;
                    }
            }
        }

        /// <summary>
        /// Показывает текущие лимиты (из appsettings.json через AppConfig)
        /// с клавиатурой выбора лимита для изменения.
        /// </summary>
        /// <param name="telegramBotClient">Клиент Telegram.</param>
        /// <param name="chat">Чат с пользователем.</param>
        /// <param name="ct">Токен отмены операции.</param>
        private async Task ShowLimitsAsync(ITelegramBotClient telegramBotClient, Chat chat,
            CancellationToken ct)
        {
            var (maxTasks, maxLengthTask, maxListsPerUser, maxRecipesPerList) = AppConfig.ReadLimits(_settingsPath);
            var str = new StringBuilder();
            str.AppendLine(" Текущие лимиты:");
            str.AppendLine($" MaxTasks: {maxTasks}");
            str.AppendLine($" MaxLengthTask: {maxLengthTask}");
            str.AppendLine($" MaxListsPerUser: {maxListsPerUser}");
            str.AppendLine($" MaxRecipesPerList: {maxRecipesPerList}");
            await telegramBotClient.SendMessage(chat, str.ToString(),
                replyMarkup: Keyboards.BuildLimitsKeyboard(), cancellationToken: ct);
        }

        /// <summary>Отправляет список всех пользователей (имя, TelegramId, роль) текстом.</summary>
        /// <param name="telegramBotClient">Клиент Telegram.</param>
        /// <param name="chat">Чат с пользователем.</param>
        /// <param name="ct">Токен отмены операции.</param>
        private async Task ListUsersAsync(ITelegramBotClient telegramBotClient, Chat chat,
            CancellationToken ct)
        {
            var users = await _userService.GetAllUsersAsync(ct);

            if (users.Count == 0)
            {
                await telegramBotClient.SendMessage(chat, " Список пользователей пуст", cancellationToken: ct);
                return;
            }

            var str = new StringBuilder("\n Список пользователей:\n");
            str.AppendJoin("\n", users.Select((u, i) => $" {i + 1}. {u.TelegramUserName} | TelegramId: {u.TelegramUserId} | State: {ToDoUser.GetStateName(u.State)}"));
            await telegramBotClient.SendMessage(chat, str.ToString(), cancellationToken: ct);
        }

        /// <summary>
        /// Отправляет список пользователей кнопками для смены роли
        /// (callback — «префикс_UserId»).
        /// </summary>
        /// <param name="telegramBotClient">Клиент Telegram.</param>
        /// <param name="chat">Чат с пользователем.</param>
        /// <param name="callbackPrefix">Префикс callback-данных (например, «setstate_member»).</param>
        /// <param name="prompt">Подсказка (какое действие выполняется).</param>
        /// <param name="ct">Токен отмены операции.</param>
        private async Task ShowUserListForStateChangeAsync(ITelegramBotClient telegramBotClient,
            Chat chat, string callbackPrefix, string prompt, CancellationToken ct)
        {
            var users = await _userService.GetAllUsersAsync(ct);

            if (users.Count == 0)
            {
                await telegramBotClient.SendMessage(chat, " Список пользователей пуст", cancellationToken: ct);
                return;
            }

            await telegramBotClient.SendMessage(chat, prompt,
                replyMarkup: Keyboards.BuildUserListKeyboard(users, callbackPrefix), cancellationToken: ct);
        }

        /// <summary>
        /// Меняет роль пользователя по кнопке «setstate_роль_UserId»:
        /// проверка прав вызывающего (Moderator — только Guest/Member;
        /// Advanced/Moderator/Admin — только Admin), затем ChangeStateAsync.
        /// </summary>
        /// <param name="telegramBotClient">Клиент Telegram.</param>
        /// <param name="chat">Чат с пользователем.</param>
        /// <param name="data">Callback-данные кнопки («setstate_роль_UserId»).</param>
        /// <param name="adminUserId">Идентификатор вызывающего (проверка прав).</param>
        /// <param name="ct">Токен отмены операции.</param>
        private async Task HandleSetStateCallbackAsync(ITelegramBotClient telegramBotClient,
            Chat chat, string data, long adminUserId, CancellationToken ct)
        {
            var parts = data.Split('_', 3);
            if (parts.Length < 3)
                return;

            string stateName = parts[1];
            if (!Guid.TryParse(parts[2], out Guid targetUserId))
            {
                var prompt1 = " Не удалось разобрать идентификатор пользователя";
                await telegramBotClient.SendMessage(chat, prompt1, cancellationToken: ct);
                return;
            }

            ToDoUser.ToDoUserState targetState = stateName switch
            {
                "guest" => ToDoUser.ToDoUserState.Guest,
                "member" => ToDoUser.ToDoUserState.Member,
                "advanced" => ToDoUser.ToDoUserState.Advanced,
                "moderator" => ToDoUser.ToDoUserState.Moderator,
                "admin" => ToDoUser.ToDoUserState.Admin,
                _ => ToDoUser.ToDoUserState.Guest
            };

            var adminUser = await _userService.GetUserAsync(adminUserId, ct);
            if (adminUser == null || (adminUser.State != ToDoUser.ToDoUserState.Moderator &&
                adminUser.State != ToDoUser.ToDoUserState.Admin))
            {
                var prompt1 = "У вас нет прав для выполнения этой операции.";
                await telegramBotClient.SendMessage(chat, prompt1, cancellationToken: ct);
                return;
            }

            if ((targetState == ToDoUser.ToDoUserState.Advanced ||
                targetState == ToDoUser.ToDoUserState.Moderator ||
                targetState == ToDoUser.ToDoUserState.Admin) &&
                adminUser.State != ToDoUser.ToDoUserState.Admin)
            {
                var prompt1 = "Требуются права Администратора.";
                await telegramBotClient.SendMessage(chat, prompt1, cancellationToken: ct);
                return;
            }

            await _userService.ChangeStateAsync(targetUserId, targetState, ct);

            var targetUser = await _userService.GetUserByUserIdAsync(targetUserId, ct);
            var userName = targetUser?.TelegramUserName ?? "Unknown";
            var prompt = $" Пользователь \"{userName}\" теперь имеет статус: {ToDoUser.GetStateName(targetState)}";
            await telegramBotClient.SendMessage(chat, prompt, cancellationToken: ct);


        }

        /// <summary>
        /// Обрабатывает ввод нового значения лимита (шаг AwaitingConfigLimit):
        /// валидация (1–1000), запись в appsettings.json, перечитывание
        /// конфигурации и обновление лимитов в сервисах.
        /// </summary>
        /// <param name="telegramBotClient">Клиент Telegram.</param>
        /// <param name="chat">Чат с пользователем.</param>
        /// <param name="userId">Идентификатор пользователя в Telegram.</param>
        /// <param name="text">Введённое значение лимита.</param>
        /// <param name="ct">Токен отмены операции.</param>
        private async Task UpdateConfigLimitAsync(ITelegramBotClient telegramBotClient, Chat chat, long userId,
            string text, CancellationToken ct)
        {
            if (!int.TryParse(text, out int value) || value < 1 || value > 1000)
            {
                await telegramBotClient.SendMessage(chat, "Введите число от 1 до 1000:", cancellationToken: ct);
                return;
            }

            if (!_configLimitTargets.TryGetValue(userId, out string? targetName) || string.IsNullOrEmpty(targetName))
                return;

            var json = File.ReadAllText(_settingsPath);
            var root = JsonNode.Parse(json)?.AsObject();
            if (root == null) return;

            root[targetName] = value;
            await File.WriteAllTextAsync(_settingsPath, root.ToString(), ct);

            var (maxTasks, maxLengthTask, maxListsPerUser, maxRecipesPerList) = AppConfig.ReadLimits(_settingsPath);
            await _todoService.SetConfigurationAsync(maxTasks, maxLengthTask, maxRecipesPerList, ct);
            await _toDoListService.SetConfigurationAsync(maxListsPerUser, ct);

            var prompt = $"{targetName} = {value}. Лимит обновлён.";
            await telegramBotClient.SendMessage(chat, prompt, cancellationToken: ct);
            await ShowLimitsAsync(telegramBotClient, chat, ct);
            _configLimitTargets.TryRemove(userId, out _);
            SetState(userId, HandlerState.Ready);
        }

        /// <summary>
        /// Очистка in-memory записей незарегистрированных пользователей
        /// (блокировки, состояния, цели смены имени/лимита) — «журнал активности».
        /// </summary>
        /// <param name="telegramBotClient">Клиент Telegram.</param>
        /// <param name="chat">Чат с пользователем.</param>
        /// <param name="ct">Токен отмены операции.</param>
        private async Task CleanupGuestLocksAsync(ITelegramBotClient telegramBotClient, Chat chat,
            CancellationToken ct)
        {
            int removed = 0;
            var keys = _userLocks.Keys.ToList();

            foreach (var key in keys)
            {
                var user = await _userService.GetUserAsync(key, ct);
                if (user == null)
                {
                    _userLocks.TryRemove(key, out _);
                    _states.TryRemove(key, out _);
                    _changeNameTargets.TryRemove(key, out _);
                    _configLimitTargets.TryRemove(key, out _);
                    removed++;
                }
            }
            var prompr = $"Очищено записей незарегистрированных пользователей: {removed}";
            await telegramBotClient.SendMessage(chat, prompr, cancellationToken: ct);
        }
    }
}
