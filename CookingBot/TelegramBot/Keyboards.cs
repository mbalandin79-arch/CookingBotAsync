using System;
using System.Linq;
using CookingBot.Core.Entities;
using CookingBot.Helpers;
using CookingBot.TelegramBot.Dto;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types;

namespace CookingBot.TelegramBot
{
    /// <summary>
    /// Построение всех клавиатур бота: BotCommand-меню, inline-клавиатуры
    /// и ReplyKeyboardMarkup «Отмена» для сценариев.
    /// Навигация бота строится ровно на трёх элементах (см. PLAN.md):
    /// BotCommand-меню по роли, «Отмена» в сценариях, inline главное меню по роли.
    /// </summary>
    public static class Keyboards
    {
        private static readonly int _pageSize = 5;

        /// <summary>
        /// Собирает BotCommand-меню по роли пользователя (для SetMyCommands):
        /// базовые команды + «Мой профиль» для зарегистрированных
        /// + «Администрирование» для модератора/администратора.
        /// </summary>
        /// <param name="user">Пользователь; null — меню для незарегистрированного.</param>
        /// <returns>Список команд BotCommand-меню.</returns>
        public static BotCommand[] GetCommandsForUser(ToDoUser? user)
        {
            var commands = new List<BotCommand>()
            {
                new BotCommand { Command = "/start", Description = "Начать работу" },
                new BotCommand { Command = "/cook", Description = "Рецепты" },
                new BotCommand { Command = "/help", Description = "Помощь" },
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

        /// <summary>
        /// Собирает inline главное меню по роли пользователя:
        /// «Рецепты» всем, «Профиль» зарегистрированным,
        /// «Админ» модератору/администратору.
        /// </summary>
        /// <param name="user">Пользователь; null — меню для незарегистрированного.</param>
        /// <returns>Клавиатура главного меню.</returns>
        public static InlineKeyboardMarkup BuildMainMenuKeyboard(ToDoUser? user)
        {
            var rows = new List<List<InlineKeyboardButton>>
            {
                new() { InlineKeyboardButton.WithCallbackData("Рецепты", "cook_menu") }
            };

            if (user != null)
            {
                rows.Add(new() { InlineKeyboardButton.WithCallbackData("Профиль", "profile_menu") });

                if (user.State == ToDoUser.ToDoUserState.Admin ||
                    user.State == ToDoUser.ToDoUserState.Moderator)
                {
                    rows.Add(new() { InlineKeyboardButton.WithCallbackData("Админ", "admin_menu") });
                }
            }

            return new InlineKeyboardMarkup(rows);
        }

        /// <summary>Собирает клавиатуру профиля: смена имени, удаление аккаунта, назад.</summary>
        /// <param name="userId">Идентификатор пользователя (в callback-данных кнопок).</param>
        /// <returns>Клавиатура профиля.</returns>
        public static InlineKeyboardMarkup BuildProfileKeyboard(Guid userId)
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Смена имени", $"changename_{userId}") },
                new[] { InlineKeyboardButton.WithCallbackData("Удалить аккаунт", $"deleteaccount_{userId}") },
                new[] { InlineKeyboardButton.WithCallbackData("Назад", "mainmenu") }
            });
        }

        /// <summary>
        /// Собирает клавиатуру меню администрирования: список пользователей,
        /// повышение/понижение ролей (только Admin — вплоть до Admin),
        /// лимиты. Moderator до внедрения экрана пользователя управляет
        /// только списком. Кнопка «Очистка журнала активности» временная:
        /// заменяется фоновой задачей чистки (п.2 плана).
        /// </summary>
        /// <param name="state">Роль вызывающего (определяет набор кнопок).</param>
        /// <returns>Клавиатура меню администрирования.</returns>
        public static InlineKeyboardMarkup BuildAdminMenuKeyboard(ToDoUser.ToDoUserState state)
        {
            var rows = new List<List<InlineKeyboardButton>>();

            rows.Add(new() { InlineKeyboardButton.WithCallbackData("Список пользователей", "mod_listusers") });           

            if (state == ToDoUser.ToDoUserState.Admin)
            {
                rows.Add(new()
                {
                    InlineKeyboardButton.WithCallbackData("Повысить до Moderator", "admin_promote_mod"),
                    InlineKeyboardButton.WithCallbackData("Понизить до Advanced", "admin_demote_advanced")
                });
                rows.Add(new()
                {
                    InlineKeyboardButton.WithCallbackData("Повысить до Admin", "admin_promote_admin"),
                    InlineKeyboardButton.WithCallbackData("Понизить до Moderator", "admin_demote_mod")
                });
                rows.Add(new()
                {
                    InlineKeyboardButton.WithCallbackData("Лимиты", "admin_limits")
                });
            }

            rows.Add(new()
            {
                InlineKeyboardButton.WithCallbackData("Очистка журнала активности", "admin_cleanup_locks")
            });
            rows.Add(new() { InlineKeyboardButton.WithCallbackData("Назад", "mainmenu") });

            return new InlineKeyboardMarkup(rows);
        }

        /// <summary>
        /// Собирает ReplyKeyboardMarkup с кнопкой «Отмена» — показывается
        /// на время выполнения сценариев (после завершения убирается).
        /// </summary>
        /// <returns>Клавиатура с кнопкой «Отмена».</returns>
        public static ReplyKeyboardMarkup BuildCancelKeyboard()
        {
            return new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { new KeyboardButton("Отмена") }
            })
            { ResizeKeyboard = true };
        }

        /// <summary>
        /// Собирает клавиатуру выбора категории рецепта (шаг «Category»
        /// сценария добавления рецепта); callback-данные — «cat_имяКатегории».
        /// </summary>
        /// <returns>Клавиатура категорий.</returns>
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

        /// <summary>
        /// Собирает клавиатуру «Пропустить» (шаг скрытых ингредиентов,
        /// callback — «cat_skip»).
        /// </summary>
        /// <returns>Клавиатура с кнопкой «Пропустить».</returns>
        public static InlineKeyboardMarkup BuildSkipKeyboard()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Пропустить", "cat_skip") }
            });
        }

        /// <summary>
        /// Собирает клавиатуру «Готово» (завершение ввода шагов приготовления,
        /// callback — «steps_done»).
        /// </summary>
        /// <returns>Клавиатура с кнопкой «Готово».</returns>
        public static InlineKeyboardMarkup BuildDoneKeyboard()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Готово", "steps_done") }
            });
        }

        /// <summary>
        /// Собирает клавиатуру выбора раздела для удаления (шаг «Approve»
        /// сценария удаления раздела); callback — «deletelist|Id».
        /// </summary>
        /// <param name="lists">Разделы пользователя.</param>
        /// <returns>Клавиатура с кнопками разделов.</returns>
        public static InlineKeyboardMarkup BuildKeyboardDeleteListForUser(List<ToDoList> lists)
        {
            var rows = lists.Select(list => new List<InlineKeyboardButton> 
            { 
                InlineKeyboardButton.WithCallbackData(list.Name, new ToDoListCallbackDto
                {
                    Action = "deletelist",
                    ToDoListId = list.Id
                }.ToString())
            }).ToList();

            return new InlineKeyboardMarkup(rows);
        }

        /// <summary>Собирает клавиатуру подтверждения Да/Нет (callback — «yes»/«no»).</summary>
        /// <returns>Клавиатура Да/Нет.</returns>
        public static InlineKeyboardMarkup BuildKeyboardYesNo()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Да", "yes") },
                new[] { InlineKeyboardButton.WithCallbackData("Нет", "no") }
            });
        }

        /// <summary>
        /// Собирает клавиатуру меню разделов: «Все рецепты», кнопки разделов
        /// («show|Id|страница»), «Добавить список» / «Удалить список».
        /// </summary>
        /// <param name="lists">Разделы пользователя.</param>
        /// <returns>Клавиатура меню разделов.</returns>
        public static InlineKeyboardMarkup BuildShowListsKeyboard(IReadOnlyList<ToDoList> lists)
        {
            var rows = lists.Select(list => new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData(list.Name, new PagedListCallbackDto
                {
                    Action = "show",
                    ToDoListId = list.Id,
                    Page = 0
                }.ToString())
            }).ToList();

            rows.Insert(0, new()
            {
                InlineKeyboardButton.WithCallbackData("Все рецепты", new PagedListCallbackDto
                {
                    Action = "show",
                    ToDoListId = null,
                    Page = 0
                }.ToString())
            });

            rows.Add(new()
            {
                InlineKeyboardButton.WithCallbackData("Добавить список", "addlist"),
                InlineKeyboardButton.WithCallbackData("Удалить список", "deletelist")
            });

            return new InlineKeyboardMarkup(rows);
        }

        /// <summary>
        /// Собирает клавиатуру выбора подкатегории при добавлении рецепта
        /// (шаг «SubCategory»): «Без подкатегории», разделы («addtask|Id»),
        /// «Создать новую».
        /// </summary>
        /// <param name="lists">Разделы пользователя.</param>
        /// <returns>Клавиатура выбора подкатегории.</returns>
        public static InlineKeyboardMarkup BuildListsKeyboard(IReadOnlyList<ToDoList> lists)
        {
            var rows = new List<List<InlineKeyboardButton>> 
            { 
                new()
                {
                    InlineKeyboardButton.WithCallbackData("Без подкатегории", new ToDoListCallbackDto
                    {
                        Action = "addtask",
                        ToDoListId = null
                    }.ToString())
                }
            };

            rows.AddRange(lists.Select(list => new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData(list.Name, new ToDoListCallbackDto
                    {
                        Action = "addtask",
                        ToDoListId = list.Id
                    }.ToString())
            }));

            rows.Add(new()
            {
                InlineKeyboardButton.WithCallbackData("Создать новую", "newlist")
            });

            return new InlineKeyboardMarkup(rows);
        }

        /// <summary>
        /// Собирает клавиатуру выбора лимита для изменения
        /// (callback — «config_имяЛимита»).
        /// </summary>
        /// <returns>Клавиатура лимитов.</returns>
        public static InlineKeyboardMarkup BuildLimitsKeyboard()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("MaxTasksMember", "config_MaxTasksMember") },
                new[] { InlineKeyboardButton.WithCallbackData("MaxTasksAdvanced", "config_MaxTasksAdvanced") },
                new[] { InlineKeyboardButton.WithCallbackData("MaxLengthTask", "config_MaxLengthTask") },
                new[] { InlineKeyboardButton.WithCallbackData("MaxListsPerUserMember", "config_MaxListsPerUserMember") },
                new[] { InlineKeyboardButton.WithCallbackData("MaxListsPerUserAdvanced", "config_MaxListsPerUserAdvanced") },
                new[] { InlineKeyboardButton.WithCallbackData("MaxRecipesPerList", "config_MaxRecipesPerList") },
                new[] { InlineKeyboardButton.WithCallbackData("Назад", "mainmenu") }
            });
        }

        /// <summary>
        /// Собирает меню раздела «Рецепты»: поиск и показ всех рецептов — всем;
        /// добавление, «найти мой», «показать мои» — только зарегистрированным.
        /// </summary>
        /// <param name="isRegistered">Признак зарегистрированного пользователя.</param>
        /// <returns>Клавиатура меню «Рецепты».</returns>
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
                    InlineKeyboardButton.WithCallbackData("Добавить рецепт", "add_recipe")
                });
                rows.Add(new() {
                    InlineKeyboardButton.WithCallbackData("Найти мой рецепт", "findmy_recipe") ,
                    InlineKeyboardButton.WithCallbackData("Показать мои рецепты", "showmy_recipes")
                });
            }

            rows.Add(new() { InlineKeyboardButton.WithCallbackData("Назад", "mainmenu") });

            return new InlineKeyboardMarkup(rows);
        }

        /// <summary>
        /// Собирает меню профиля: профиль, смена имени, статистика,
        /// удаление аккаунта, назад (Id пользователя — в callback-данных).
        /// </summary>
        /// <param name="userId">Идентификатор пользователя.</param>
        /// <returns>Клавиатура меню профиля.</returns>
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

        /// <summary>
        /// Собирает клавиатуру подтверждения регистрации Да/Нет
        /// (callback — «reg_yes»/«reg_no»).
        /// </summary>
        /// <returns>Клавиатура Да/Нет.</returns>
        public static InlineKeyboardMarkup BuildRegistrationKeyboard()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Да", "reg_yes") },
                new[] { InlineKeyboardButton.WithCallbackData("Нет", "reg_no") }
            });
        }

        /// <summary>
        /// Собирает клавиатуру подтверждения удаления аккаунта
        /// («Да, удалить» — «confirmdelete_Id», «Отмена»).
        /// </summary>
        /// <param name="userId">Идентификатор пользователя.</param>
        /// <returns>Клавиатура подтверждения удаления.</returns>
        public static InlineKeyboardMarkup BuildDeleteAccountKeyboard(Guid userId)
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Да, удалить", $"confirmdelete_{userId}") },
                new[] { InlineKeyboardButton.WithCallbackData("Отмена", "mainmenu") }
            });
        }

        /// <summary>
        /// Собирает inline-клавиатуру «Отмена» (перегрузка для inline-меню;
        /// callback по умолчанию — «mainmenu»).
        /// </summary>
        /// <param name="callbackData">Callback-данные кнопки «Отмена».</param>
        /// <returns>Клавиатура с кнопкой «Отмена».</returns>
        public static InlineKeyboardMarkup BuildCancelKeyboard(string callbackData = "mainmenu")
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Отмена", callbackData) }
            });
        }

        /// <summary>
        /// Собирает клавиатуру выбора имени при регистрации:
        /// «Оставить по умолчанию» (reg_default), «Отмена» (reg_no).
        /// </summary>
        /// <returns>Клавиатура выбора имени.</returns>
        public static InlineKeyboardMarkup BuildRegistrationNameKeyboard()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Оставить по умолчанию", "reg_default") },
                new[] { InlineKeyboardButton.WithCallbackData("Отмена", "reg_no") }
            });
        }

        /// <summary>
        /// Собирает клавиатуру действий с рецептом (карточка рецепта):
        /// «Удалить» (deletetask), назад.
        /// </summary>
        /// <param name="taskId">Идентификатор рецепта.</param>
        /// <returns>Клавиатура действий с рецептом.</returns>
        public static InlineKeyboardMarkup BuildTaskActionKeyboard(Guid taskId)
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("❌Удалить", new ToDoItemCallbackDto { Action = "deletetask", ToDoItemId = taskId }.ToString()) },
                new[] { InlineKeyboardButton.WithCallbackData("Назад", "mainmenu") }
            });
        }

        /// <summary>
        /// Собирает клавиатуру только с кнопкой «Главное меню» —
        /// карточка рецепта в режиме «только чтение» (для гостей).
        /// </summary>
        /// <returns>Клавиатура с кнопкой «Главное меню».</returns>
        public static InlineKeyboardMarkup BuildReadOnlyTaskKeyboard()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Главное меню", "mainmenu") }
            });
        }

        /// <summary>
        /// Собирает клавиатуру списка пользователей (администрирование):
        /// кнопки «Имя (роль)» с callback «префикс_UserId» + «Главное меню».
        /// </summary>
        /// <param name="users">Пользователи для показа.</param>
        /// <param name="callbackPrefix">Префикс callback-данных (какое действие выполняет кнопка).</param>
        /// <returns>Клавиатура списка пользователей.</returns>
        public static InlineKeyboardMarkup BuildUserListKeyboard(IReadOnlyList<ToDoUser> users, string callbackPrefix)
        {
            var rows = users.Select(u => new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData($"{u.TelegramUserName} ({ToDoUser.GetStateName(u.State)})", $"{callbackPrefix}_{u.UserId}")
            }).ToList();

            rows.Add(new() { InlineKeyboardButton.WithCallbackData("Главное меню", "mainmenu") });
            return new InlineKeyboardMarkup(rows);
        }

        /// <summary>
        /// Строит постраничную inline-клавиатуру: кнопки-элементы (по 5 на страницу),
        /// стрелки навигации, необязательная дополнительная кнопка и кнопка "Главное меню".
        /// </summary>
        /// <param name="callbackData">Список кнопок: текст -> callback-данные.</param>
        /// <param name="listDto">Текущее состояние постраничного вывода (действие, список, страница).</param>
        /// <param name="extraButton">Необязательная дополнительная кнопка внизу (например, "Посмотреть выполненные").</param>
        public static InlineKeyboardMarkup BuildPagedButtons(IReadOnlyList<KeyValuePair<string, string>> callbackData,
            PagedListCallbackDto listDto, KeyValuePair<string, string>? extraButton = null)
        {
            int totalPages = (int)Math.Ceiling((double)callbackData.Count / _pageSize);
            if (totalPages == 0) totalPages = 1;

            var rows = callbackData.GetBatchByNumber(_pageSize, listDto.Page).Select(item => new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData(item.Key, item.Value)
            }).ToList();

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
    }
}
