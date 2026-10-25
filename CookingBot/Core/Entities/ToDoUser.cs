using System;

namespace CookingBot.Core.Entities
{
    /// <summary>
    /// Пользователь бота.
    /// Core-сущность: с ней работает бизнес-логика (сервисы и сценарии);
    /// в базе данных хранится через модель ToDoUserModel (Core/DataAccess/Models),
    /// преобразование выполняет ModelMapper.
    /// </summary>
    public class ToDoUser
    {
        /// <summary>
        /// Уровень доступа пользователя: определяет набор доступных команд
        /// (BotCommand-меню и главное меню строятся по роли).
        /// </summary>
        public enum ToDoUserState
        {
            /// <summary>Гость.</summary>
            Guest,
            /// <summary>Участник.</summary>
            Member,
            /// <summary>Активный участник.</summary>
            Advanced,
            /// <summary>Модератор.</summary>
            Moderator,
            /// <summary>Администратор.</summary>
            Admin
        }
        /// <summary>Уникальный идентификатор пользователя (первичный ключ в БД).</summary>
        public Guid UserId { get; set; }
        /// <summary>Имя пользователя в Telegram.</summary>
        public string TelegramUserName { get; set; } = null!;
        /// <summary>Дата и время регистрации пользователя.</summary>
        public DateTime RegisteredAt { get; set; }
        /// <summary>Идентификатор пользователя в Telegram.</summary>
        public long TelegramUserId { get; set; }
        /// <summary>Уровень доступа пользователя (роль).</summary>
        public ToDoUserState State { get; set; }

        /// <summary>
        /// Возвращает название уровня доступа на русском языке для отображения пользователю.
        /// </summary>
        /// <param name="state">Уровень доступа.</param>
        /// <returns>Русское название уровня доступа; для неизвестного значения — его строковое представление.</returns>
        public static string GetStateName(ToDoUserState state)
        {
            return state switch
            {
                ToDoUserState.Guest => "Гость",
                ToDoUserState.Member => "Участник",
                ToDoUserState.Advanced => "Активный участник",
                ToDoUserState.Moderator => "Модератор",
                ToDoUserState.Admin => "Администратор",
                _ => state.ToString()
            };
        }
    }
}
