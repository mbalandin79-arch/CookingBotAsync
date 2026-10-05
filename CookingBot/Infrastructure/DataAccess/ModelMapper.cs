using CookingBot.Core.DataAccess.Models;
using CookingBot.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        public static ToDoItem MapFromModel(ToDoItemModel model)
        {
            return new ToDoItem
            {
                Id = model.Id,
                User = model.User != null ? MapFromModel(model.User) : null!,
                Name = model.Name,
                Steps = model.Steps.ToList(),
                CreatedAt = model.CreatedAt,
                State = model.State,
                StateChangedAt = model.StateChangedAt,
                Category = model.Category,
                Ingredients = model.Ingredients.ToList(),
                HiddenIngredients = model.HiddenIngredients.ToList(),
                List = model.List != null ? MapFromModel(model.List) : null
            };
        }

        public static ToDoItemModel MapToModel(ToDoItem entity)
        {
            return new ToDoItemModel
            {
                Id = entity.Id,
                Name = entity.Name,
                CreatedAt = entity.CreatedAt,
                State = entity.State,
                StateChangedAt = entity.StateChangedAt,
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
    }
}
