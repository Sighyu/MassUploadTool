using MassUploadTool.Models;

namespace MassUploadTool.Managers
{
    public static class BatchManager
    {
        public static List<Batch> CreateBatches(List<FileItem> files, long maxBatchSize, int maxFilesPerBatch)
        {
            files.Sort((a, b) => b.Size.CompareTo(a.Size));
            var batches = new List<Batch>();
            foreach (var file in files)
            {
                bool placed = false;
                foreach (var batch in batches)
                {
                    if (batch.Files.Count < maxFilesPerBatch && batch.TotalSize + file.Size <= maxBatchSize)
                    {
                        batch.Files.Add(file);
                        batch.TotalSize += file.Size;
                        placed = true;
                        break;
                    }
                }
                if (!placed)
                {
                    var newBatch = new Batch();
                    newBatch.Files.Add(file);
                    newBatch.TotalSize = file.Size;
                    batches.Add(newBatch);
                }
            }
            return batches;
        }
    }
}
