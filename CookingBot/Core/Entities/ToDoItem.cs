using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using static CookingBot.Core.Entities.ToDoUser;

namespace CookingBot.Core.Entities
{
    public class ToDoItem
    {
        public enum ToDoItemState
        {
            Active,
            Completed
        }

        public enum MainCategory
        {
            Other,
            Soup,
            Salat,
            Main,
            Dessert,
            Drink,
            Bakery,
            Breakfast,
            Sauce
        }

        public Guid Id { get; }
        public ToDoUser User { get; }
        public string Name { get; set; }
        public List<string> Steps { get; set; }
        public DateTime CreatedAt { get; }
        public ToDoItemState State { get; set; }
        public DateTime? StateChangedAt { get; set; }

        // TODO: В финальной версии убрать, если не найдём применение для рецепта.
        // Оставлено по требованию ДЗ.
        public DateTime Deadline { get; set; }
        public MainCategory Category { get; set; }
        public string? SubCategory { get; set; }
        public List<string> Ingredients { get; set; }
        public List<string> HiddenIngredients { get; set; }
        public ToDoList? List { get; }


        public ToDoItem(ToDoUser user, string name, DateTime deadline, MainCategory category, string? subCategory, List<string> ingredients, List<string> hiddenIngredients, List<string> steps, ToDoList? list)
        {
            User = user;
            Name = name;
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow; // универсальная дата и время на данный момент для всех часовых поясов
            State = ToDoItemState.Active;
            Deadline = deadline;
            Category = category;
            SubCategory = subCategory;
            Ingredients = ingredients;
            HiddenIngredients = hiddenIngredients;
            Steps = steps;
            List = list;
        }

        [JsonConstructor]
        public ToDoItem(Guid id, ToDoUser user, string name, List<string> steps, DateTime createdAt, ToDoItemState state, DateTime? stateChangedAt, DateTime deadline, MainCategory category, string? subCategory, List<string> ingredients, List<string> hiddenIngredients, ToDoList? list)
        {
            Id = id;
            User = user;
            Name = name;
            Steps = steps;
            CreatedAt = createdAt;
            State = state;
            StateChangedAt = stateChangedAt;
            Deadline = deadline;
            Category = category;
            SubCategory = subCategory;
            Ingredients = ingredients;
            HiddenIngredients = hiddenIngredients;
            List = list;
        }

        public static string GetCategoryName(MainCategory category)
        {
            return category switch
            {
                MainCategory.Other => "Другое",
                MainCategory.Soup => "Суп",
                MainCategory.Salat => "Салат",
                MainCategory.Main => "Основное блюдо",
                MainCategory.Dessert => "Десерт",
                MainCategory.Drink => "Напиток",
                MainCategory.Bakery => "Выпечка",
                MainCategory.Breakfast => "Завтрак",
                MainCategory.Sauce => "Соус",
                _ => category.ToString()
            };
        }

        public static string GetStateName(ToDoItemState state)
        {
            return state switch
            {
                ToDoItemState.Active => "Активна",
                ToDoItemState.Completed => "Завершена",
                _ => state.ToString()
            };
        }
    }
}
