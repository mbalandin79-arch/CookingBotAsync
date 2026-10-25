using System;

namespace CookingBot.Core.Entities
{
    /// <summary>
    /// Раздел кулинарной книги (подкатегория рецептов).
    /// Core-сущность: с ней работает бизнес-логика (сервисы и сценарии);
    /// в базе данных хранится через модель ToDoListModel (Core/DataAccess/Models),
    /// преобразование выполняет ModelMapper.
    /// </summary>
    public class ToDoList
    {
        /// <summary>Уникальный идентификатор раздела (первичный ключ в БД).</summary>
        public Guid Id { get; set; }
        /// <summary>Название раздела. Заполняется при создании в сервисе или при маппинге из БД.</summary>
        public string Name { get; set; } = null!;
        /// <summary>Владелец раздела. Заполняется при создании в сервисе или при маппинге из БД.</summary>
        public ToDoUser User { get; set; } = null!;
        /// <summary>Дата и время создания раздела.</summary>
        public DateTime CreatedAt { get; set; }
    }
}
