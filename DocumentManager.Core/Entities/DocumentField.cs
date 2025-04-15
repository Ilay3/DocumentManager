using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentManager.Core.Entities
{
    /// <summary>
    /// Поле документа
    /// </summary>
    public class DocumentField
    {
        public int Id { get; set; }
        public int DocumentTemplateId { get; set; }
        public string FieldName { get; set; } // Техническое имя поля
        public string FieldLabel { get; set; } // Отображаемое имя поля
        public string FieldType { get; set; } // Тип поля (text, date, select и т.д.)
        public bool IsRequired { get; set; }
        public bool IsUnique { get; set; }
        public string? DefaultValue { get; set; }
        public string? Options { get; set; } // JSON-строка с опциями для select
        public string? Condition { get; set; } // JSON-строка с условием отображения

        // Навигационные свойства
        public DocumentTemplate DocumentTemplate { get; set; }
    }

}
