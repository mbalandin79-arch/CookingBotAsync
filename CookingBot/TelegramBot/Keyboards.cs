using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types.ReplyMarkups;
using CookingBot.Core.Entities;
using System.Net.WebSockets;
using CookingBot.TelegramBot.Dto;
using System.Collections;

namespace CookingBot.TelegramBot
{
    public static class Keyboards
    {
        public static InlineKeyboardMarkup BuildKeyboardForUser(ToDoUser? user)
        {
            var rows = new List<List<InlineKeyboardButton>>();

            if (user == null)
            {
                rows.Add(new()
                {
                    InlineKeyboardButton.WithCallbackData("Старт", "/start"),
                    InlineKeyboardButton.WithCallbackData("Помощь", "/help")
                });
                rows.Add(new()
                {
                    InlineKeyboardButton.WithCallbackData("Все задачи (общие)", "/alltasks"),
                    InlineKeyboardButton.WithCallbackData("Поиск (общие)", "/findall")
                });
                rows.Add(new()
                {
                    InlineKeyboardButton.WithCallbackData("Информация", "/info")
                });

                return new InlineKeyboardMarkup(rows);
            }

            rows.Add(new()
            {
                InlineKeyboardButton.WithCallbackData("Добавить задачу", "/addtask")
                //InlineKeyboardButton.WithCallbackData("Активные задачи", "/showtasks")
            });
            rows.Add(new()
            {
                InlineKeyboardButton.WithCallbackData("Все задачи", "/show"),
                //InlineKeyboardButton.WithCallbackData("Все задачи", "/showalltasks"),
                InlineKeyboardButton.WithCallbackData("Инфо о задаче", "/infotask")
            });
            rows.Add(new()
            {
                InlineKeyboardButton.WithCallbackData("Все задачи (общие)", "/alltasks"),
                InlineKeyboardButton.WithCallbackData("Поиск (общие)", "/findall")
            });
            rows.Add(new()
            {
                InlineKeyboardButton.WithCallbackData("Удалить задачу", "/removetask"),
                InlineKeyboardButton.WithCallbackData("Завершить задачу", "/completetask")
            });
            rows.Add(new()
            {
                InlineKeyboardButton.WithCallbackData("Поиск", "/find"),
                InlineKeyboardButton.WithCallbackData("Отчёт", "/report")
            });
            rows.Add(new()
            {
                InlineKeyboardButton.WithCallbackData("Мой профиль", "/myinfo"),
                InlineKeyboardButton.WithCallbackData("Помощь", "/help")
            });
            rows.Add(new()
            {
                InlineKeyboardButton.WithCallbackData("Информация", "/info"),
                InlineKeyboardButton.WithCallbackData("Выход", "/exit")
            });

            if (user.State == ToDoUser.ToDoUserState.Moderator || user.State == ToDoUser.ToDoUserState.Admin)
            {
                rows.Add(new()
                {
                    InlineKeyboardButton.WithCallbackData("Список пользователей", "mod_listusers")
                });
                rows.Add(new()
                {
                    InlineKeyboardButton.WithCallbackData("Повысить до Member", "mod_promote_member"),
                    InlineKeyboardButton.WithCallbackData("Понизить до Guest", "mod_demote_guest")
                });
            }

            if (user.State == ToDoUser.ToDoUserState.Admin)
            {
                rows.Add(new()
                {
                    InlineKeyboardButton.WithCallbackData("Повысить до Moderator", "admin_promote_mod"),
                    InlineKeyboardButton.WithCallbackData("Повысить до Admin", "admin_promote_admin")
                });
                rows.Add(new()
                {
                    InlineKeyboardButton.WithCallbackData("Понизить до Advanced", "admin_demote_advanced"),
                    InlineKeyboardButton.WithCallbackData("Понизить до Moderator", "admin_demote_mod")
                });
            }

            return new InlineKeyboardMarkup(rows);
        }

        public static InlineKeyboardMarkup BuildProfileKeyboard(Guid userId)
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Смена имени", $"changename_{userId}") },
                new[] { InlineKeyboardButton.WithCallbackData("Удалить аккаунт", $"deleteaccount_{userId}") },
                new[] { InlineKeyboardButton.WithCallbackData("Назад", "mainmenu") }
            });
        }

        public static ReplyKeyboardMarkup BuildCancelKeyboard()
        {
            return new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { new KeyboardButton("Отмена") }
            })
            { ResizeKeyboard = true };
        }

        public static ReplyKeyboardMarkup BuildMainReplyKeyboard()
        {
            return new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { new KeyboardButton("/addtask"), new KeyboardButton("/show") },
                new KeyboardButton[] { new KeyboardButton("/report"), new KeyboardButton("/help") }
            })
            { ResizeKeyboard = true };
        }

        public static InlineKeyboardMarkup BuildCategoryKeyboard()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Другое", "cat_Other") },
                new[] { InlineKeyboardButton.WithCallbackData("Суп", "cat_Soup") },
                new[] { InlineKeyboardButton.WithCallbackData("Салат", "cat_Salat") },
                new[] { InlineKeyboardButton.WithCallbackData("Основное блюдо", "cat_Main") },
                new[] { InlineKeyboardButton.WithCallbackData("Десерт", "cat_Dessert") },
                new[] { InlineKeyboardButton.WithCallbackData("Напиток", "cat_Drink") },
                new[] { InlineKeyboardButton.WithCallbackData("Выпечка", "cat_Bakery") },
                new[] { InlineKeyboardButton.WithCallbackData("Завтрак", "cat_Breakfast") },
                new[] { InlineKeyboardButton.WithCallbackData("Соус", "cat_Sauce") }
            });
        }

        public static InlineKeyboardMarkup BuildSkipKeyboard()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Пропустить", "cat_skip") }
            });
        }

        public static InlineKeyboardMarkup BuildDoneKeyboard()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Готово", "steps_done") }
            });
        }

        public static InlineKeyboardMarkup BuildKeyboardDeleteListForUser(List<ToDoList> lists)
        {
            var rows = new List<List<InlineKeyboardButton>>();

            foreach (var list in lists)
            {
                var callbackData = new ToDoListCallbackDto
                {
                    Action = "deletelist",
                    ToDoListId = list.Id
                }.ToString();

                rows.Add(new()
                {
                    InlineKeyboardButton.WithCallbackData(list.Name, callbackData)
                });
            }

            return new InlineKeyboardMarkup(rows);
        }

        public static InlineKeyboardMarkup BuildKeyboardYesNo()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Да", "yes") },
                new[] { InlineKeyboardButton.WithCallbackData("Нет", "no") }
            });
        }

        public static InlineKeyboardMarkup BuildShowListsKeyboard(IReadOnlyList<ToDoList> lists)
        {
            var rows = new List<List<InlineKeyboardButton>>();

            rows.Add(new()
            {
                InlineKeyboardButton.WithCallbackData("Все рецепты", new ToDoListCallbackDto
                {
                    Action = "show",
                    ToDoListId = null
                }.ToString())
            });


            foreach (var list in lists)
            {
                rows.Add(new()
                {
                    InlineKeyboardButton.WithCallbackData(list.Name, new ToDoListCallbackDto
                    {
                        Action = "show",
                        ToDoListId = list.Id
                    }.ToString())
                });
            }

            rows.Add(new()
            {
                InlineKeyboardButton.WithCallbackData("Добавить список", "/addlist"),
                InlineKeyboardButton.WithCallbackData("Удалить список", "/deletelist")
            });

            return new InlineKeyboardMarkup(rows);
        }

        public static InlineKeyboardMarkup BuildListsKeyboard(IReadOnlyList<ToDoList> lists)
        {
            var rows = new List<List<InlineKeyboardButton>>();

            rows.Add(new()
            {
                InlineKeyboardButton.WithCallbackData("Без списка", new ToDoListCallbackDto
                {
                    Action = "addtask",
                    ToDoListId = null
                }.ToString())
            });


            foreach (var list in lists)
            {
                rows.Add(new()
                {
                    InlineKeyboardButton.WithCallbackData(list.Name, new ToDoListCallbackDto
                    {
                        Action = "addtask",
                        ToDoListId = list.Id
                    }.ToString())
                });
            }

            return new InlineKeyboardMarkup(rows);
        }
    }
}
