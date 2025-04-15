using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentManager.Core.Interfaces
{
    /// <summary>
    /// Сервис для работы с JSON-схемами полей
    /// </summary>
    public interface IJsonSchemaService
    {
        /// <summary>
        /// Загрузить JSON-схему полей из файла
        /// </summary>
        Task<Dictionary<string, object>> LoadJsonSchemaAsync(string jsonPath);

        /// <summary>
        /// Получить поля из JSON-схемы
        /// </summary>
        IEnumerable<Dictionary<string, object>> GetFieldsFromSchema(Dictionary<string, object> schema);

        /// <summary>
        /// Проверить значение поля на соответствие схеме
        /// </summary>
        bool ValidateFieldValue(Dictionary<string, object> fieldSchema, string value);

        /// <summary>
        /// Получить условие отображения поля
        /// </summary>
        Dictionary<string, object> GetFieldCondition(Dictionary<string, object> fieldSchema);
    }
}
