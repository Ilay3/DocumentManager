namespace DocumentManager.Web.Models
{
    public class DocumentFieldViewModel
    {
        public int Id { get; set; }
        public string FieldName { get; set; }
        public string FieldLabel { get; set; }
        public string FieldType { get; set; }
        public bool IsRequired { get; set; }
        public bool IsUnique { get; set; }
        public string DefaultValue { get; set; }
        public List<string> Options { get; set; } = new List<string>();
        public Dictionary<string, string> Condition { get; set; }
        public string Value { get; set; }
    }

}
