using System;

namespace CookingBot.Core.Entities
{
    /// <summary>
    /// Рецепт кулинарной книги.
    /// Core-сущность: с ней работает бизнес-логика (сервисы и сценарии);
    /// в базе данных хранится через модель ToDoItemModel (Core/DataAccess/Models),
    /// преобразование выполняет ModelMapper.
    /// </summary>
    public class ToDoItem
    {
        /// <summary>Состояние рецепта: в работе или завершён.</summary>
        public enum ToDoItemState
        {
            /// <summary>Рецепт в работе.</summary>
            Active,
            /// <summary>Рецепт завершён.</summary>
            Completed
        }

        /// <summary>
        /// Категория рецепта — крупный раздел кулинарной книги
        /// (суп, салат, основное блюдо и т.д.).
        /// </summary>
        public enum MainCategory
        {
            /// <summary>Без категории.</summary>
            Other,
            /// <summary>Суп.</summary>
            Soup,
            /// <summary>Салат.</summary>
            Salat,
            /// <summary>Основное блюдо.</summary>
            Main,
            /// <summary>Десерт.</summary>
            Dessert,
            /// <summary>Напиток.</summary>
            Drink,
            /// <summary>Выпечка.</summary>
            Bakery,
            /// <summary>Завтрак.</summary>
            Breakfast,
            /// <summary>Соус.</summary>
            Sauce
        }

        /// <summary>Уникальный идентификатор рецепта (первичный ключ в БД).</summary>
        public Guid Id { get; set; }
        /// <summary>Автор рецепта. Заполняется при создании в сервисе или при маппинге из БД.</summary>
        public ToDoUser User { get; set; } = null!;
        /// <summary>Название рецепта. Заполняется при создании в сервисе или при маппинге из БД.</summary>
        public string Name { get; set; } = null!;
        /// <summary>Шаги приготовления рецепта.</summary>
        public List<string> Steps { get; set; } = new List<string>();
        /// <summary>Дата и время создания рецепта.</summary>
        public DateTime CreatedAt { get; set; }
        /// <summary>Состояние рецепта (в работе / завершён).</summary>
        public ToDoItemState State { get; set; }
        /// <summary>Дата и время смены состояния рецепта (UTC); null, если состояние не менялось.</summary>
        public DateTime? StateChangedAt { get; set; }
        /// <summary>Категория рецепта — раздел кулинарной книги.</summary>
        public MainCategory Category { get; set; }
        /// <summary>Ингредиенты рецепта: по ним возможен поиск.</summary>
        public List<string> Ingredients { get; set; } = new List<string>();
        /// <summary>Скрытые ингредиенты: поиск по ним недоступен (в карточке рецепта показываются наравне с обычными).</summary>
        public List<string> HiddenIngredients { get; set; } = new List<string>();
        /// <summary>
        /// Подкатегория (раздел кулинарной книги), к которой относится рецепт.
        /// null — рецепт без подкатегории.
        /// </summary>
        public ToDoList? List { get; set; }

        /// <summary>
        /// Возвращает название категории на русском языке для отображения пользователю.
        /// </summary>
        /// <param name="category">Категория рецепта.</param>
        /// <returns>Русское название категории; для неизвестного значения — его строковое представление.</returns>
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

        /// <summary>
        /// Возвращает название состояния рецепта на русском языке для отображения пользователю.
        /// </summary>
        /// <param name="state">Состояние рецепта.</param>
        /// <returns>Русское название состояния; для неизвестного значения — его строковое представление.</returns>
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
