using CookingBot.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LinqToDB.Mapping;

namespace CookingBot.Core.DataAccess.Models
{
    [Table("ToDoList")]
    public class ToDoListModel
    {
        [PrimaryKey]
        [Column("Id")]
        public Guid Id { get; set; }

        [Column("Name")]
        public string Name { get; set; } = null!;

        // Столбец внешнего ключа: автор рецепта
        [Column("UserId")]
        public Guid UserId { get; set; }

        [Association(ThisKey = nameof(UserId), OtherKey = nameof(ToDoUserModel.UserId))]
        public ToDoUserModel User { get; set; } = null!;

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; }
    }
}
