using DocumentManager.Infrastructure.Services;

namespace DocumentManager.Web.Models
{
    public class DirectoryTreeViewModel
    {
        public List<DirectoryInfoViewModel> Directories { get; set; } = new List<DirectoryInfoViewModel>();
        public List<FileInfoViewModel> Files { get; set; } = new List<FileInfoViewModel>();
    }

    public class DirectoryInfoViewModel
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public bool HasSubdirectories { get; set; }
    }

    public class FileInfoViewModel
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public bool HasJsonSchema { get; set; }
    }

    public class GenerateJsonRequest
    {
        public List<string> FilePaths { get; set; } = new List<string>();
    }

    public class GenerationResultViewModel
    {
        public List<JsonGenerationResult> Results { get; set; } = new List<JsonGenerationResult>();
        public int SuccessCount => Results.Count(r => r.Success);
        public int ErrorCount => Results.Count(r => !r.Success);
    }
}