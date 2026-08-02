using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using CookingBot.Core.DataAccess;
using CookingBot.Core.Entities;
using CookingBot.Core.Exceptions;

namespace CookingBot.Infrastructure.DataAccess
{
    internal class InMemoryToDoRepository : IToDoRepository
    {
        private List<ToDoItem> _toDoItems = new List<ToDoItem>();

        public async Task AddAsync(ToDoItem item)
        {
            _toDoItems.Add(item);
        }

        public async Task<int> CountActiveAsync(Guid userId)
        {            
            return _toDoItems.Where(w => w.User.UserId == userId && w.State == ToDoItem.ToDoItemState.Active).Count();
        }

        public async Task DeleteAsync(Guid id)
        {
            _toDoItems.Remove(_toDoItems.First(f => f.Id == id));
        }

        public async Task<bool> ExistsByNameAsync(Guid userId, string name)
        {
            foreach (var curr in _toDoItems)
            {
                if (curr.User.UserId == userId && curr.Name == name)
                    return true;
            }
            return false;
        }

        public async Task<ToDoItem?> GetAsync(Guid id)
        {
            return _toDoItems.FirstOrDefault(curr => curr.Id == id);
        }

        public async Task<IReadOnlyList<ToDoItem>> GetActiveByUserIdAsync(Guid userId)
        {
            List<ToDoItem> listTemp = new List<ToDoItem>();

            foreach (var curr in _toDoItems)
            {
                if (curr.User.UserId == userId && curr.State == ToDoItem.ToDoItemState.Active)
                    listTemp.Add(curr);
            }
            return listTemp;
        }

        public async Task<IReadOnlyList<ToDoItem>> GetAllByUserIdAsync(Guid userId)
        {
            List<ToDoItem> listTemp = new List<ToDoItem>();

            foreach (var curr in _toDoItems)
            {
                if (curr.User.UserId == userId)
                    listTemp.Add(curr);
            }
            return listTemp;
        }        

        public async Task UpdateAsync(ToDoItem item)
        {
            var index = _toDoItems.IndexOf(_toDoItems.FirstOrDefault(f => f.Id == item.Id));
            _toDoItems[index] = item;
        }

        public async Task<IReadOnlyList<ToDoItem>> FindAsync(Guid userId, Func<ToDoItem, bool> predicate)
        {
            return _toDoItems.Where(f => predicate(f) && f.User.UserId == userId).ToList();
        }
    }
}
