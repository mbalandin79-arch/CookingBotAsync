-- ============================================================
-- ToDoListDb.sql
-- Схема базы данных "ToDoList" для проекта CookingBot (Кулинарный бот)
-- СУБД: PostgreSQL 18
--
-- Содержимое:
--   1. Удаление таблиц (для повторного запуска скрипта)
--   2. Таблицы: "ToDoUser", "ToDoList", "ToDoItem", "Notification"
--   3. Внешние ключи (связи между таблицами)
--   4. Индексы
--
-- Как запускать:
--   1) Создайте базу данных:  CREATE DATABASE "ToDoList";
--   2) Подключитесь к базе "ToDoList" и выполните этот файл целиком.
--
-- Примечание: двойные кавычки вокруг имён таблиц, столбцов и
-- индексов сохраняют регистр букв.
-- ============================================================

-- ------------------------------------------------------------
-- 1. Удаление таблиц, если они уже существуют.
-- Нужно, чтобы скрипт можно было запускать повторно.
-- ВНИМАНИЕ: это уничтожит все данные в таблицах!
-- Порядок: сначала дочерние таблицы (FK), потом родительские.
-- ------------------------------------------------------------
DROP TABLE IF EXISTS "Notification";
DROP TABLE IF EXISTS "ToDoItem";
DROP TABLE IF EXISTS "ToDoList";
DROP TABLE IF EXISTS "ToDoUser";

-- ------------------------------------------------------------
-- 2. Таблицы
-- ------------------------------------------------------------

-- Пользователи бота (из сущности ToDoUser)
CREATE TABLE "ToDoUser"
(
    "UserId"           UUID        NOT NULL,  -- PRIMARY KEY; Guid из C#
    "TelegramUserId"   BIGINT      NOT NULL,  -- Id пользователя в Telegram (long)
    "TelegramUserName" TEXT        NOT NULL,  -- отображаемое имя
    "RegisteredAt"     TIMESTAMPTZ NOT NULL,  -- дата регистрации (в UTC)
    "State"            INT         NOT NULL,  -- роль: 0=Guest, 1=Member, 2=Advanced, 3=Moderator, 4=Admin

    CONSTRAINT "PK_ToDoUser" PRIMARY KEY ("UserId")
);

-- Списки рецептов / подкатегории (из сущности ToDoList)
CREATE TABLE "ToDoList"
(
    "Id"        UUID        NOT NULL,  -- PRIMARY KEY
    "Name"      TEXT        NOT NULL,  -- название списка (лимит длины проверяет приложение)
    "UserId"    UUID        NOT NULL,  -- владелец списка -> "ToDoUser"."UserId"
    "CreatedAt" TIMESTAMPTZ NOT NULL,  -- дата создания (в UTC)

    CONSTRAINT "PK_ToDoList" PRIMARY KEY ("Id")
);

-- Рецепты (из сущности ToDoItem)
CREATE TABLE "ToDoItem"
(
    "Id"                UUID        NOT NULL,  -- PRIMARY KEY
    "Name"              TEXT        NOT NULL,  -- название рецепта
    "Steps"             TEXT[]      NOT NULL,  -- шаги приготовления (список строк)
    "CreatedAt"         TIMESTAMPTZ NOT NULL,  -- дата создания (в UTC)
    "State"             INT         NOT NULL,  -- состояние: 0=Active, 1=Completed (ToDoItemState)
    "StateChangedAt"    TIMESTAMPTZ NULL,      -- когда изменилось состояние (NULL - не менялось)
    "Category"          INT         NOT NULL,  -- категория (MainCategory):
                                               --   0=Other, 1=Soup, 2=Salat, 3=Main, 4=Dessert,
                                               --   5=Drink, 6=Bakery, 7=Breakfast, 8=Sauce
    "Ingredients"       TEXT[]      NOT NULL,  -- ингредиенты (по ним доступен поиск)
    "HiddenIngredients" TEXT[]      NOT NULL,  -- скрытые ингредиенты (поиск недоступен)
    "UserId"            UUID        NOT NULL,  -- автор рецепта -> "ToDoUser"."UserId"
    "ListId"            UUID        NULL,      -- список (подкатегория) -> "ToDoList"."Id"; NULL = без списка
    "Deadline"          TIMESTAMPTZ NULL,      -- дедлайн рецепта, дата планируемого приготовления (NULL = нет дедлайна)

    CONSTRAINT "PK_ToDoItem" PRIMARY KEY ("Id")
);

-- Нотификации (из сущности Notification, ДЗ17)
CREATE TABLE "Notification"
(
    "Id"          UUID        NOT NULL,  -- PRIMARY KEY
    "UserId"      UUID        NOT NULL,  -- получатель -> "ToDoUser"."UserId"
    "Type"        TEXT        NOT NULL,  -- тип нотификации (например: DeadLine_{Id рецепта}, Today_{дата})
    "Text"        TEXT        NOT NULL,  -- текст, который будет отправлен
    "ScheduledAt" TIMESTAMPTZ NOT NULL,  -- запланированная дата отправки (в UTC)
    "IsNotified"  BOOLEAN     NOT NULL,  -- флаг отправки: false - ждёт отправки
    "NotifiedAt"  TIMESTAMPTZ NULL,      -- фактическая дата отправки (NULL - не отправлена)

    CONSTRAINT "PK_Notification" PRIMARY KEY ("Id")
);

-- ------------------------------------------------------------
-- 3. Внешние ключи (связи между таблицами)
-- ------------------------------------------------------------

-- У каждого списка один владелец; у пользователя много списков.
ALTER TABLE "ToDoList" ADD CONSTRAINT "FK_ToDoList_ToDoUser"
    FOREIGN KEY ("UserId") REFERENCES "ToDoUser" ("UserId") ON DELETE CASCADE;

-- У каждого рецепта один автор; у пользователя много рецептов.
ALTER TABLE "ToDoItem" ADD CONSTRAINT "FK_ToDoItem_ToDoUser"
    FOREIGN KEY ("UserId") REFERENCES "ToDoUser" ("UserId") ON DELETE CASCADE;

-- Рецепт может принадлежать одному списку или ни одному (ListId = NULL);
-- при удалении списка его рецепты удаляются автоматически.
ALTER TABLE "ToDoItem" ADD CONSTRAINT "FK_ToDoItem_ToDoList"
    FOREIGN KEY ("ListId") REFERENCES "ToDoList" ("Id") ON DELETE CASCADE;

-- У каждой нотификации один получатель; у пользователя много нотификаций.
-- При удалении пользователя его нотификации удаляются автоматически.
ALTER TABLE "Notification" ADD CONSTRAINT "FK_Notification_ToDoUser"
    FOREIGN KEY ("UserId") REFERENCES "ToDoUser" ("UserId") ON DELETE CASCADE;

-- ------------------------------------------------------------
-- 4. Индексы
-- ------------------------------------------------------------

-- Индексы на внешние ключи: ускоряют выборки вида
-- "все списки пользователя", "все рецепты пользователя", "рецепты списка".
-- (PostgreSQL НЕ создаёт индексы на внешние ключи автоматически -
--  в отличие от первичного ключа, который индексируется сам.)
CREATE INDEX "IX_ToDoList_UserId"  ON "ToDoList" ("UserId");
CREATE INDEX "IX_ToDoItem_UserId"  ON "ToDoItem" ("UserId");
CREATE INDEX "IX_ToDoItem_ListId"  ON "ToDoItem" ("ListId");

-- Индекс на UserId таблицы Notification (требование ДЗ17):
-- ускоряет выборку "нотификации пользователя" и работу FK при удалении.
CREATE INDEX "IX_Notification_UserId" ON "Notification" ("UserId");

-- Уникальный индекс: один Telegram-аккаунт = один пользователь.
-- База сама не даст завести двух пользователей с одним TelegramUserId.
CREATE UNIQUE INDEX "UX_ToDoUser_TelegramUserId" ON "ToDoUser" ("TelegramUserId");
