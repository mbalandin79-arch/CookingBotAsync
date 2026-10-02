-- ============================================================
-- Содержимое:
--   1. Удаление таблиц (для повторного запуска скрипта)
--   2. Таблицы: "ToDoUser", "ToDoList", "ToDoItem"
--   3. Внешние ключи (связи между таблицами)
--   4. Индексы
-- ============================================================

-- ------------------------------------------------------------
-- 1. Удаление таблиц, если они уже существуют.
-- Нужно, чтобы скрипт можно было запускать повторно.
-- ВНИМАНИЕ: это уничтожит все данные в таблицах!
-- ------------------------------------------------------------
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

    CONSTRAINT "PK_ToDoItem" PRIMARY KEY ("Id")
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

-- Уникальный индекс: один Telegram-аккаунт = один пользователь.
-- База сама не даст завести двух пользователей с одним TelegramUserId.
CREATE UNIQUE INDEX "UX_ToDoUser_TelegramUserId" ON "ToDoUser" ("TelegramUserId");
