-- ============================================================
-- ToDoListDb_Select.sql
-- SQL-скрипты для методов интерфейса IToDoRepository,
-- которые выбирают данные.
-- СУБД: PostgreSQL 18
--
-- Как запускать:
--   Подключитесь к базе "ToDoList", где выполнены 01_ToDoListDb.sql
--   и 02_ToDoListDb_Insert.sql, и выполняйте запросы (каждый отдельно
--   или файлом целиком).
--
-- В каждом запросе комментарием указан метод интерфейса,
-- который он реализует.
-- ============================================================

-- ------------------------------------------------------------
-- Базовые выборки
-- ------------------------------------------------------------

-- GetAllAsync: все рецепты всех пользователей
SELECT * FROM "ToDoItem"
ORDER BY "CreatedAt";

-- GetAllByUserIdAsync(userId): все рецепты пользователя
-- (подставлен UserId Михаила из ToDoListDb_Insert.sql)
SELECT * FROM "ToDoItem"
WHERE "UserId" = '00000000-0000-0000-0000-000000000001'
ORDER BY "CreatedAt";

-- GetActiveByUserIdAsync(userId): только активные рецепты пользователя
SELECT * FROM "ToDoItem"
WHERE "UserId" = '00000000-0000-0000-0000-000000000001'
  AND "State" = 0
ORDER BY "CreatedAt";

-- GetAsync(id): один рецепт по Id (Сырники)
SELECT * FROM "ToDoItem"
WHERE "Id" = '00000000-0000-0000-0002-000000000001';

-- ExistsByNameAsync(userId, name): проверка имени без учёта регистра.
-- EXISTS возвращает true/false - "есть ли хотя бы одна такая строка".
-- lower(...) = lower(...) - аналог OrdinalIgnoreCase из C#.
SELECT EXISTS (
    SELECT 1 FROM "ToDoItem"
    WHERE "UserId" = '00000000-0000-0000-0000-000000000001'
      AND lower("Name") = lower('сырники')
) AS "Существует";

-- CountActiveAsync(userId): число активных рецептов пользователя
SELECT count(*) AS "Активных"
FROM "ToDoItem"
WHERE "UserId" = '00000000-0000-0000-0000-000000000001'
  AND "State" = 0;

-- ------------------------------------------------------------
-- Поиск
-- ------------------------------------------------------------

-- FindAllAsync(predicate): все рецепты, удовлетворяющие условию.
-- Предикат в C# - произвольная функция; в SQL её аналог - условие в WHERE.
-- Пример предиката "имя начинается с 'с'": ILIKE - это LIKE без учёта регистра.
SELECT * FROM "ToDoItem"
WHERE "Name" ILIKE 'с%'
ORDER BY "CreatedAt";

-- FindAsync(userId, predicate): то же, но только рецепты конкретного пользователя
SELECT * FROM "ToDoItem"
WHERE "UserId" = '00000000-0000-0000-0000-000000000002'
  AND "Name" ILIKE 'г%'
ORDER BY "CreatedAt";

-- FindByNameContainsAsync(namePart): имя содержит часть строки
SELECT * FROM "ToDoItem"
WHERE "Name" ILIKE '%ник%'
ORDER BY "CreatedAt";

-- FindByCategoryAsync(category): по категории (6 = Выпечка)
SELECT * FROM "ToDoItem"
WHERE "Category" = 6
ORDER BY "CreatedAt";

-- FindByIngredientAsync(ingredient): по ингредиенту.
-- "= ANY(...)" читается "равен хотя бы одному элементу массива"
SELECT * FROM "ToDoItem"
WHERE 'мука' = ANY ("Ingredients")
ORDER BY "CreatedAt";

-- FindByIngredientAsync без учёта регистра (так ищет бот):
-- unnest(...) разворачивает массив в набор строк, по ним - обычное условие
SELECT * FROM "ToDoItem"
WHERE EXISTS (
    SELECT 1 FROM unnest("Ingredients") AS ing
    WHERE lower(ing) = lower('Мука')
)
ORDER BY "CreatedAt";

-- ------------------------------------------------------------
-- ДЗ17: фоновые задачи и нотификации
-- ------------------------------------------------------------

-- GetActiveWithDeadline(userId, from, to): активные рецепты пользователя,
-- у которых дедлайн попадает в интервал [from, to).
-- Пример для DeadlineBackgroundTask (дедлайны "вчера"):
--   from = '2026-10-26', to = '2026-10-27' (даты UTC).
SELECT * FROM "ToDoItem"
WHERE "UserId" = '00000000-0000-0000-0000-000000000002'
  AND "State" = 0
  AND "Deadline" IS NOT NULL
  AND "Deadline" >= '2026-10-26'
  AND "Deadline" < '2026-10-27'
ORDER BY "CreatedAt";

-- Пример для TodayBackgroundTask (дедлайны "сегодня"):
--   from = '2026-10-27', to = '2026-10-28'.
SELECT * FROM "ToDoItem"
WHERE "State" = 0
  AND "Deadline" IS NOT NULL
  AND "Deadline" >= '2026-10-27'
  AND "Deadline" < '2026-10-28'
ORDER BY "CreatedAt";

-- GetAllUsersAsync(): все пользователи (для перебора в фоновых задачах)
SELECT * FROM "ToDoUser"
ORDER BY "TelegramUserName";

-- ------------------------------------------------------------
-- ДЗ17: NotificationService (нотификации)
-- ------------------------------------------------------------

-- ScheduleNotification - проверка дубля (userId + type):
-- EXISTS возвращает true, если нотификация такого типа уже есть
SELECT EXISTS (
    SELECT 1 FROM "Notification"
    WHERE "UserId" = '00000000-0000-0000-0000-000000000001'
      AND "Type" = 'Deadline_00000000-0000-0000-0002-000000000004'
) AS "Дубль";

-- GetScheduledNotification(scheduledBefore): неотправленные нотификации,
-- время которых наступило (их рассылает NotificationBackgroundTask)
SELECT * FROM "Notification"
WHERE "IsNotified" = false
  AND "ScheduledAt" <= now() AT TIME ZONE 'UTC'
ORDER BY "ScheduledAt";

-- Справочно: MarkNotified после успешной отправки выполняет
-- UPDATE "Notification" SET "IsNotified" = true, "NotifiedAt" = <текущее время UTC>
-- WHERE "Id" = <Id нотификации>. Здесь не выполняется: этот файл -
-- только выборки, данные не меняет.
