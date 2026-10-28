
namespace CookingBot.Helpers
{
    /// <summary>
    /// Сведения о версии программы. Единственное место, где версия и дата
    /// релиза задаются вручную;
    /// команда /help печатает их из этого класса.
    /// Нумерация версий: сейчас - 1.1, далее +0.1 за этап работ.
    /// </summary>
    internal static class VersionInfo
    {
        /// <summary>Номер версии программы.</summary>
        public const string Version = "1.1";

        /// <summary>Дата создания проекта.</summary>
        public const string CreationDate = "21.05.2026";
    }
}
