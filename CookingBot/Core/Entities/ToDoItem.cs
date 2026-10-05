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

        public Guid Id { get; set; }
        public ToDoUser User { get; set; }
        public string Name { get; set; }
        public List<string> Steps { get; set; }
        public DateTime CreatedAt { get; set; }
        public ToDoItemState State { get; set; }
        public DateTime? StateChangedAt { get; set; }
        public MainCategory Category { get; set; }
        public List<string> Ingredients { get; set; }
        public List<string> HiddenIngredients { get; set; }
        public ToDoList? List { get; set; }

        public static string GetCategoryName(MainCategory category)
        {
            return category switch
            {                
                MainCategory.Soup => "Суп",
                MainCategory.Salat => "Салат",
                MainCategory.Main => "Основное блюдо",
                MainCategory.Dessert => "Десерт",
                MainCategory.Drink => "Напиток",
                MainCategory.Bakery => "Выпечка",
                MainCategory.Breakfast => "Завтрак",
                MainCategory.Sauce => "Соус",
                MainCategory.Other => "Без категории",
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
