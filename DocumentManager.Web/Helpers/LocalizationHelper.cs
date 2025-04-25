namespace DocumentManager.Web.Helpers
{
    public static class LocalizationHelper
    {
        // Словарь для локализации типов документов
        private static readonly Dictionary<string, string> DocumentTypes = new Dictionary<string, string>
        {
            { "Passport", "Паспорт" },
            { "PackingList", "Упаковочный лист" },
            { "PackingInventory", "Упаковочная ведомость" },
            { "Other", "Другой тип" }
        };

        // Словарь для локализации типов полей
        private static readonly Dictionary<string, string> FieldTypes = new Dictionary<string, string>
        {
            { "text", "Текст" },
            { "date", "Дата" },
            { "select", "Список" },
            { "number", "Число" },
            { "checkbox", "Флажок" }
        };

        // Получить локализованное название типа документа
        public static string GetLocalizedDocumentType(string type)
        {
            return DocumentTypes.TryGetValue(type, out string localizedType)
                ? localizedType
                : type;
        }

        // Получить локализованное название типа поля
        public static string GetLocalizedFieldType(string fieldType)
        {
            return FieldTypes.TryGetValue(fieldType, out string localizedType)
                ? localizedType
                : fieldType;
        }
    }
}