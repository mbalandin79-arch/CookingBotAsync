using CookingBot.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LinqToDB.Mapping;
using static CookingBot.Core.Entities.ToDoItem;

namespace CookingBot.Core.DataAccess.Models
{
    [Table("ToDoItem")]
    public class ToDoItemModel
    {
        [PrimaryKey]
        [Column("Id")]
        public Guid Id { get; set; }

        // Столбец внешнего ключа: автор рецепта
        [Column("UserId")]
        public Guid UserId { get; set; }

        [Association(ThisKey = nameof(UserId), OtherKey = nameof(ToDoUserModel.UserId))]
        public ToDoUserModel User { get; set; } = null!;

        [Column("Name")]
        public string Name { get; set; } = null!;

        [Column("Steps")]
        public string[] Steps { get; set; } = Array.Empty<string>();

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; }

        [Column("State")]
        public ToDoItemState State { get; set; }

        [Column("StateChangedAt")]
        public DateTime? StateChangedAt { get; set; }

        [Column("Category")]
        public MainCategory Category { get; set; }

        [Column("Ingredients")]
        public string[] Ingredients { get; set; } = Array.Empty<string>();

        [Column("HiddenIngredients")]
        public string[] HiddenIngredients { get; set; } = Array.Empty<string>();

        // Столбец внешнего ключа: список (подкатегория), может быть null
        [Column("ListId")]
        public Guid? ListId { get; set; }

        [Association(ThisKey = nameof(ListId), OtherKey = nameof(ToDoListModel.Id))]
        public ToDoListModel? List { get; set; }
    }
}
