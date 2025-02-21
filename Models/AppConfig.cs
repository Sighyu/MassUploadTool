namespace MassUploadTool.Models
{
    public class AppConfig
    {
        public string DiscordToken { get; set; } = "";
        public List<string> AllowedFileExtensions { get; set; } = new List<string>();
        public long MaxBatchSize { get; set; } = 500L * 1024 * 1024;
        public int MaxFilesPerBatch { get; set; } = 10;
    }
}
