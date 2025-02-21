namespace MassUploadTool.Models
{
    public class FileItem
    {
        public string Path { get; set; } = "";
        public string FileName { get; set; } = "";
        public long Size { get; set; }
        public double ToMegabytes() => Size / (1024.0 * 1024.0);
    }
}
