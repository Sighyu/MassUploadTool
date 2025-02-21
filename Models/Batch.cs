namespace MassUploadTool.Models
{
    public class Batch
    {
        public List<FileItem> Files { get; set; } = new List<FileItem>();
        public long TotalSize { get; set; } = 0;
        public double TotalSizeInMB => TotalSize / (1024.0 * 1024.0);
    }
}
