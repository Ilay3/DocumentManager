using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentManager.Core.Entities
{
    /// <summary>
    /// Значение поля документа
    /// </summary>
    public class DocumentValue
    {
        public int Id { get; set; }
        public int DocumentId { get; set; }
        public int DocumentFieldId { get; set; }
        public string Value { get; set; }

        // Навигационные свойства
        public Document Document { get; set; }
        public DocumentField DocumentField { get; set; }
    }

}
