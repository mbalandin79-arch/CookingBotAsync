using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CookingBot.Core.Entities;
using static CookingBot.Core.Entities.ToDoItem;
using LinqToDB.Mapping;


namespace CookingBot.Core.DataAccess.Models
{
    /// <summary>
    /// Модель таблицы ToDoItem для linq2db (отображение рецепта в БД).
    /// Преобразование в Core-сущность ToDoItem и обратно выполняет ModelMapper.
    /// Атрибуты Association (User, List) заполняются linq2db только при LoadWith.
    /// </summary>
    [Table("ToDoItem")]
    public class ToDoItemModel
    {
        /// <summary>Уникальный идентификатор рецепта (первичный ключ).</summary>
        [PrimaryKey]
        [Column("Id")]
        public Guid Id { get; set; }

        /// <summary>Столбец внешнего ключа: автор рецепта.</summary>
        [Column("UserId")]
        public Guid UserId { get; set; }

        /// <summary>Автор рецепта (ассоциация по UserId; заполняется при LoadWith).</summary>
        [Association(ThisKey = nameof(UserId), OtherKey = nameof(ToDoUserModel.UserId))]
        public ToDoUserModel User { get; set; } = null!;

        /// <summary>Название рецепта.</summary>
        [Column("Name")]
        public string Name { get; set; } = null!;

        /// <summary>Шаги приготовления (столбец TEXT[]).</summary>
        [Column("Steps")]
        public string[] Steps { get; set; } = Array.Empty<string>();

        /// <summary>Дата и время создания рецепта.</summary>
        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; }

        /// <summary>Состояние рецепта (в работе / завершён).</summary>
        [Column("State")]
        public ToDoItemState State { get; set; }

        /// <summary>Дата и время смены состояния (UTC); null, если состояние не менялось.</summary>
        [Column("StateChangedAt")]
        public DateTime? StateChangedAt { get; set; }

        /// <summary>Категория рецепта — раздел кулинарной книги.</summary>
        [Column("Category")]
        public MainCategory Category { get; set; }

        /// <summary>Ингредиенты рецепта, по которым возможен поиск (столбец TEXT[]).</summary>
        [Column("Ingredients")]
        public string[] Ingredients { get; set; } = Array.Empty<string>();

        /// <summary>Скрытые ингредиенты: поиск недоступен, в карточке показываются (столбец TEXT[]).</summary>
        [Column("HiddenIngredients")]
        public string[] HiddenIngredients { get; set; } = Array.Empty<string>();

        /// <summary>Столбец внешнего ключа: раздел (подкатегория); null — рецепт без раздела.</summary>
        [Column("ListId")]
        public Guid? ListId { get; set; }

        /// <summary>Раздел рецепта (ассоциация по ListId; заполняется при LoadWith; null — без раздела).</summary>
        [Association(ThisKey = nameof(ListId), OtherKey = nameof(ToDoListModel.Id))]
        public ToDoListModel? List { get; set; }

        /// <summary>Дедлайн рецепта (в UTC); null — дедлайн не задан.</summary>
        [Column("Deadline")]
        public DateTime? Deadline { get; set; }
    }
}
