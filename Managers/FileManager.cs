using MassUploadTool.Models;
using Serilog;

namespace MassUploadTool.Managers
{
    public static class FileManager
    {
        public static List<FileItem> ScanFiles(List<string> directories, List<string> allowedExtensions)
        {
            var files = new List<FileItem>();
            foreach (var dir in directories)
            {
                if (Directory.Exists(dir))
                {
                    var dirFiles = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories)
                        .Where(f => allowedExtensions.Count == 0 || allowedExtensions.Contains(Path.GetExtension(f).ToLower()))
                        .Select(f => new FileItem
                        {
                            Path = f,
                            FileName = Path.GetFileName(f),
                            Size = new FileInfo(f).Length
                        });
                    files.AddRange(dirFiles);
                }
                else
                {
                    Log.Warning("Directory does not exist: {Dir}", dir);
                }
            }
            return files;
        }
    }
}
