using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.WebSockets;
using CookingBot.Core.Entities;
using CookingBot.Helpers;
using CookingBot.TelegramBot.Dto;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types;

namespace CookingBot.TelegramBot
{
    public static class Keyboards
    {
        public static BotCommand[] GetCommandsForUser(ToDoUser? user)
        {
            var commands = new List<BotCommand>()
            {
                new BotCommand { Command = "/start", Description = "Начать работу" },
                new BotCommand { Command = "/cook", Description = "Рецепты" },
                new BotCommand { Command = "/help", Description = "Помощь" },
                new BotCommand { Command = "/info", Description = "О боте"},
                new BotCommand { Command = "/exit", Description = "Завершить сессию" },
            };

            if (user != null)
            {
                commands.Insert(2, new BotCommand { Command = "/my", Description = "Мой профиль" });

                if (user.State == ToDoUser.ToDoUserState.Admin || user.State == ToDoUser.ToDoUserState.Moderator)
                {
                    commands.Insert(3, new BotCommand { Command = "/admin", Description = "Администрирование" });
                }
            }

            return commands.ToArray();
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

        public static InlineKeyboardMarkup BuildAdminMenuKeyboard()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Список пользователей", "mod_listusers") },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Повысить до Member", "mod_promote_member"),
                    InlineKeyboardButton.WithCallbackData("Понизить до Guest", "mod_demote_guest")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Повысить до Moderator", "admin_promote_mod"),
                    InlineKeyboardButton.WithCallbackData("Повысить до Admin", "admin_promote_admin")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Понизить до Advanced", "admin_demote_advanced"),
                    InlineKeyboardButton.WithCallbackData("Понизить до Moderator", "admin_demote_mod")
                },
                new[] 
                { 
                    InlineKeyboardButton.WithCallbackData("Лимиты", "admin_limits"),
                    InlineKeyboardButton.WithCallbackData("Очистка журнала активности", "admin_cleanup_locks")
                },
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

        public static InlineKeyboardMarkup BuildDeleteTaskKeyboard(Guid taskId)
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Да, удалить", $"confirmdeletetask|{taskId}") },
                new[] { InlineKeyboardButton.WithCallbackData("Отмена", "mainmenu") }
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
                InlineKeyboardButton.WithCallbackData("Без подкатегории", new ToDoListCallbackDto
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

            rows.Add(new()
            {
                InlineKeyboardButton.WithCallbackData("Создать новую", "newlist")
            });

            return new InlineKeyboardMarkup(rows);
        }

        public static InlineKeyboardMarkup BuildLimitsKeyboard()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("MaxTasks", "config_MaxTasks") },
                new[] { InlineKeyboardButton.WithCallbackData("MaxLengthTask", "config_MaxLengthTask") },
                new[] { InlineKeyboardButton.WithCallbackData("MaxListsPerUser", "config_MaxListsPerUser") },
                new[] { InlineKeyboardButton.WithCallbackData("MaxRecipesPerList", "config_MaxRecipesPerList") },
                new[] { InlineKeyboardButton.WithCallbackData("Назад", "mainmenu") }
            });
        }

        public static InlineKeyboardMarkup BuildCookMenuKeyboard(bool isRegistered)
        {
            var rows = new List<List<InlineKeyboardButton>>
            {
                new() { InlineKeyboardButton.WithCallbackData("Найти рецепт", "findall_recipe") },
                new() { InlineKeyboardButton.WithCallbackData("Показать все рецепты", "showall_recipes") }
            };

            if (isRegistered)
            {
                rows.Add(new() {
                    InlineKeyboardButton.WithCallbackData("Добавить рецепт", "add_recipe"),
                    InlineKeyboardButton.WithCallbackData("Удалить рецепт", "del_recipe")
                });
                rows.Add(new() {
                    InlineKeyboardButton.WithCallbackData("Найти мой рецепт", "findmy_recipe") ,
                    InlineKeyboardButton.WithCallbackData("Показать мои рецепты", "showmy_recipes")
                });
            }

            rows.Add(new() { InlineKeyboardButton.WithCallbackData("Назад", "mainmenu") });

            return new InlineKeyboardMarkup(rows);
        }

        public static InlineKeyboardMarkup BuildProfileMenuKeyboard(Guid userId)
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Профиль", $"profil_{userId}") },
                new[] { InlineKeyboardButton.WithCallbackData("Смена имени", $"changename_{userId}") },
                new[] { InlineKeyboardButton.WithCallbackData("Статистика", $"show_report_{userId}") },
                new[] { InlineKeyboardButton.WithCallbackData("Удалить аккаунт", $"deleteaccount_{userId}") },
                new[] { InlineKeyboardButton.WithCallbackData("Назад", "mainmenu") }
            });
        }

        public static InlineKeyboardMarkup BuildTaskListKeyboard(IReadOnlyList<ToDoItem> items)
        {
            var rows = new List<List<InlineKeyboardButton>>();
            foreach (var item in items)
            {
                var callbackData = new ToDoItemCallbackDto
                {
                    Action = "showtask",
                    ToDoItemId = item.Id
                }.ToString();
                rows.Add(new() { InlineKeyboardButton.WithCallbackData(item.Name, callbackData) });
            }
            rows.Add(new() { InlineKeyboardButton.WithCallbackData("Главное меню", "mainmenu") });
            return new InlineKeyboardMarkup(rows);
        }

        public static InlineKeyboardMarkup BuildRegistrationKeyboard()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Да", "reg_yes") },
                new[] { InlineKeyboardButton.WithCallbackData("Нет", "reg_no") }
            });
        }

        public static InlineKeyboardMarkup BuildDeleteAccountKeyboard(Guid userId)
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Да, удалить", $"confirmdelete_{userId}") },
                new[] { InlineKeyboardButton.WithCallbackData("Отмена", "mainmenu") }
            });
        }

        public static InlineKeyboardMarkup BuildCancelKeyboard(string callbackData = "mainmenu")
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Отмена", callbackData) }
            });
        }

        public static InlineKeyboardMarkup BuildRegistrationNameKeyboard()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Оставить по умолчанию", "reg_default") },
                new[] { InlineKeyboardButton.WithCallbackData("Отмена", "reg_no") }
            });
        }

        public static InlineKeyboardMarkup BuildTaskActionKeyboard(Guid taskId)
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("✅Выполнить", new ToDoItemCallbackDto { Action = "completetask", ToDoItemId = taskId }.ToString()) },
                new[] { InlineKeyboardButton.WithCallbackData("❌Удалить", new ToDoItemCallbackDto { Action = "deletetask", ToDoItemId = taskId }.ToString()) },
                new[] { InlineKeyboardButton.WithCallbackData("Назад", "mainmenu") }
            });
        }

        public static InlineKeyboardMarkup BuildPagedButtons(IReadOnlyList<KeyValuePair<string, string>> callbackData, PagedListCallbackDto listDto, KeyValuePair<string, string>? extraButton = null)
        {
            int pageSize = 5;
            int totalPages = (int)Math.Ceiling((double)callbackData.Count / pageSize);
            if (totalPages == 0) totalPages = 1;

            var pageItems = callbackData.GetBatchByNumber(pageSize, listDto.Page).ToList();

            var rows = new List<List<InlineKeyboardButton>>();

            foreach (var item in pageItems)
            {
                rows.Add(new() { InlineKeyboardButton.WithCallbackData(item.Key, item.Value) });
            }

            var navRow = new List<InlineKeyboardButton>();
            if (listDto.Page > 0)
            {
                navRow.Add(InlineKeyboardButton.WithCallbackData("⬅️", new PagedListCallbackDto
                {
                    Action = listDto.Action,
                    ToDoListId = listDto.ToDoListId,
                    Page = listDto.Page - 1
                }.ToString()));
            }
            if (listDto.Page < totalPages - 1)
            {
                navRow.Add(InlineKeyboardButton.WithCallbackData("➡️", new PagedListCallbackDto
                {
                    Action = listDto.Action,
                    ToDoListId = listDto.ToDoListId,
                    Page = listDto.Page + 1
                }.ToString()));
            }
            if (navRow.Count > 0)
            {
                rows.Add(navRow);
            }

            if (extraButton.HasValue)
            {
                rows.Add(new() { InlineKeyboardButton.WithCallbackData(extraButton.Value.Key, extraButton.Value.Value) });
            }

            rows.Add(new() { InlineKeyboardButton.WithCallbackData("Главное меню", "mainmenu") });

            return new InlineKeyboardMarkup(rows);
        }

        public static InlineKeyboardMarkup BuildUserListKeyboard(IReadOnlyList<ToDoUser> users, string callbackPrefix)
        {
            var rows = new List<List<InlineKeyboardButton>>();
            foreach (var u in users)
            {
                var buttonText = $"{u.TelegramUserName} ({u.State})";
                var callbackData = $"{callbackPrefix}_{u.UserId}";
                rows.Add(new() { InlineKeyboardButton.WithCallbackData(buttonText, callbackData) });
            }
            rows.Add(new() { InlineKeyboardButton.WithCallbackData("Главное меню", "mainmenu") });
            return new InlineKeyboardMarkup(rows);
        }
    }
}
