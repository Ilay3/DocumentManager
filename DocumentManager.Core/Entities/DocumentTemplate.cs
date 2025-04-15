using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace DocumentManager.Core.Entities
{
    /// <summary>
    /// Шаблон документа
    /// </summary>
    public class DocumentTemplate
    {
        public int Id { get; set; }
        public string Code { get; set; } // Например, "ЭРЧМ30Т3.00.00.000-10"
        public string Name { get; set; } // Например, "ЭРЧМ30Т3.00.00.000-10 ПС Регулятор"
        public string Type { get; set; } // Тип (Паспорт, Упаковочный лист и т.д.)
        public string WordTemplatePath { get; set; } // Путь к шаблону Word
        public string JsonSchemaPath { get; set; } // Путь к JSON-схеме полей
        public bool IsActive { get; set; } = true;

        // Навигационные свойства
        public List<DocumentField> Fields { get; set; } = new List<DocumentField>();
        public List<Document> Documents { get; set; } = new List<Document>();
    }

}
