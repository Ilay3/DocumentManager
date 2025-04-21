namespace DocumentManager.Web.Models
{
    public class PrintViewModel
    {
        public int DocumentId { get; set; }
        public string DocumentName { get; set; }
        public string FactoryNumber { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; }
        public string DocumentType { get; set; }
        public long FileSize { get; set; }
    }
}
