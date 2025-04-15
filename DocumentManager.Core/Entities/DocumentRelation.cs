using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentManager.Core.Entities
{
    /// <summary>
    /// Связь между документами (например, паспорт и упаковочные листы)
    /// </summary>
    public class DocumentRelation
    {
        public int Id { get; set; }
        public int ParentDocumentId { get; set; }
        public int ChildDocumentId { get; set; }

        // Навигационные свойства
        public Document ParentDocument { get; set; }
        public Document ChildDocument { get; set; }
    }

}
