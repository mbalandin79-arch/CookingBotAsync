using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CookingBot.Core.DataAccess.Models;
using CookingBot.Core.Entities;
using static CookingBot.Core.Entities.ToDoItem;
using static CookingBot.Core.Entities.ToDoUser;

namespace CookingBot.Infrastructure.DataAccess
{
    /// <summary>
    /// Конвертирует модели БД (linq2db) в Core-сущности и обратно.
    /// Нужен, чтобы Core не знал про БД и linq2db: сервисы и бот
    /// продолжают работать с привычными сущностями ToDoItem/ToDoList/ToDoUser.
    /// </summary>
    internal static class ModelMapper
    {
        /// <summary>Преобразует модель пользователя в Core-сущность.</summary>
        /// <param name="model">Модель из БД.</param>
        /// <returns>Core-сущность пользователя.</returns>
        public static ToDoUser MapFromModel(ToDoUserModel model)
        {
            return new ToDoUser
            {
                UserId = model.UserId,
                TelegramUserName = model.TelegramUserName,
                RegisteredAt = model.RegisteredAt,
                TelegramUserId = model.TelegramUserId,
                State = model.State
            };
        }

        /// <summary>Преобразует Core-сущность пользователя в модель БД.</summary>
        /// <param name="entity">Core-сущность.</param>
        /// <returns>Модель для записи в БД.</returns>
        public static ToDoUserModel MapToModel(ToDoUser entity)
        {
            return new ToDoUserModel
            {
                UserId = entity.UserId,
                TelegramUserName = entity.TelegramUserName,
                RegisteredAt = entity.RegisteredAt,
                TelegramUserId = entity.TelegramUserId,
                State = entity.State
            };
        }

        /// <summary>
        /// Преобразует модель рецепта в Core-сущность.
        /// Массивы string[] (TEXT[]) превращаются в List&lt;string&gt;;
        /// ассоциации User и List копируются рекурсивно, если они заполнены
        /// (заполняются linq2db только при LoadWith).
        /// </summary>
        /// <param name="model">Модель из БД.</param>
        /// <returns>Core-сущность рецепта.</returns>
        public static ToDoItem MapFromModel(ToDoItemModel model)
        {
            return new ToDoItem
            {
                Id = model.Id,
                User = model.User != null ? MapFromModel(model.User) : null!,
                Name = model.Name,
                Steps = model.Steps.ToList(),
                CreatedAt = model.CreatedAt,
                Category = model.Category,
                Ingredients = model.Ingredients.ToList(),
                HiddenIngredients = model.HiddenIngredients.ToList(),
                List = model.List != null ? MapFromModel(model.List) : null
            };
        }

        /// <summary>
        /// Преобразует Core-сущность рецепта в модель БД.
        /// Контракт: сущность должна быть с заполненным User (UserId берётся из него);
        /// ListId — null, если рецепт без раздела. Списки List&lt;string&gt;
        /// превращаются в массивы string[] (пустые вместо null).
        /// </summary>
        /// <param name="entity">Core-сущность (с заполненным User).</param>
        /// <returns>Модель для записи в БД.</returns>
        public static ToDoItemModel MapToModel(ToDoItem entity)
        {
            return new ToDoItemModel
            {
                Id = entity.Id,
                Name = entity.Name,
                CreatedAt = entity.CreatedAt,
                Category = entity.Category,
                Steps = entity.Steps?.ToArray() ?? Array.Empty<string>(),
                Ingredients = entity.Ingredients?.ToArray() ?? Array.Empty<string>(),
                HiddenIngredients = entity.HiddenIngredients?.ToArray() ?? Array.Empty<string>(),
                UserId = entity.User!.UserId,
                User = entity.User != null ? MapToModel(entity.User) : null!,
                ListId = entity.List?.Id,
                List = entity.List != null ? MapToModel(entity.List) : null
            };
        }

        /// <summary>
        /// Преобразует модель раздела в Core-сущность.
        /// Ассоциация User копируется рекурсивно, если заполнена (LoadWith).
        /// </summary>
        /// <param name="model">Модель из БД.</param>
        /// <returns>Core-сущность раздела.</returns>
        public static ToDoList MapFromModel(ToDoListModel model)
        {
            return new ToDoList
            {
                Id = model.Id,
                Name = model.Name,
                User = model.User != null ? MapFromModel(model.User) : null!,
                CreatedAt = model.CreatedAt
            };
        }

        /// <summary>
        /// Преобразует Core-сущность раздела в модель БД.
        /// Контракт: сущность должна быть с заполненным User (UserId берётся из него).
        /// </summary>
        /// <param name="entity">Core-сущность (с заполненным User).</param>
        /// <returns>Модель для записи в БД.</returns>
        public static ToDoListModel MapToModel(ToDoList entity)
        {
            return new ToDoListModel
            {
                Id = entity.Id,
                Name = entity.Name,
                CreatedAt = entity.CreatedAt,
                UserId = entity.User!.UserId,
                User = entity.User != null ? MapToModel(entity.User) : null!
            };
        }

        /// <summary>
        /// Преобразует модель нотификации в Core-сущность.
        /// Ассоциация User копируется рекурсивно, если заполнена (LoadWith).
        /// </summary>
        /// <param name="model">Модель из БД.</param>
        /// <returns>Core-сущность нотификации.</returns>
        public static Notification MapFromModel(NotificationModel model)
        {
            return new Notification
            {
                Id = model.Id,
                User = model.User != null ? MapFromModel(model.User) : null!,
                Type = model.Type,
                Text = model.Text,
                ScheduledAt = model.ScheduledAt,
                IsNotified = model.IsNotified,
                NotifiedAt = model.NotifiedAt
            };
        }
    }
}
