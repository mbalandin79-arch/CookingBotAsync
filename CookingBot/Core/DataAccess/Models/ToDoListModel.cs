using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CookingBot.Core.Entities;
using LinqToDB.Mapping;

namespace CookingBot.Core.DataAccess.Models
{
    /// <summary>
    /// Модель таблицы ToDoList для linq2db (отображение раздела кулинарной книги в БД).
    /// Преобразование в Core-сущность ToDoList и обратно выполняет ModelMapper.
    /// Атрибут Association (User) заполняется linq2db только при LoadWith.
    /// </summary>
    [Table("ToDoList")]
    public class ToDoListModel
    {
        /// <summary>Уникальный идентификатор раздела (первичный ключ).</summary>
        [PrimaryKey]
        [Column("Id")]
        public Guid Id { get; set; }

        /// <summary>Название раздела.</summary>
        [Column("Name")]
        public string Name { get; set; } = null!;

        /// <summary>Столбец внешнего ключа: владелец раздела.</summary>
        [Column("UserId")]
        public Guid UserId { get; set; }

        /// <summary>Владелец раздела (ассоциация по UserId; заполняется при LoadWith).</summary>
        [Association(ThisKey = nameof(UserId), OtherKey = nameof(ToDoUserModel.UserId))]
        public ToDoUserModel User { get; set; } = null!;

        /// <summary>Дата и время создания раздела.</summary>
        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; }
    }
}
