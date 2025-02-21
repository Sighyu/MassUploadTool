using System.Text.Json;
using System.Text.Json;
using MassUploadTool.Managers;
using MassUploadTool.Models;
using Serilog;

namespace MassUploadTool
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File("log.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            Log.Information("Starting Mass Upload Tool");

            string configPath = "appsettings.json";
            AppConfig config = ConfigManager.LoadConfiguration(configPath);
            if (config == null)
                config = new AppConfig();

            if (string.IsNullOrWhiteSpace(config.DiscordToken))
            {
                Console.WriteLine("Please enter your Discord token:");
                config.DiscordToken = Console.ReadLine()?.Trim() ?? "";
                config.AllowedFileExtensions = new List<string> { ".jpg", ".png", ".pdf" };
                ConfigManager.SaveConfiguration(configPath, config);
                Console.WriteLine("Configuration has been updated with your Discord token and default allowed file extensions.");
                Console.WriteLine("Please update the configuration file as needed and restart the application.");
                return;
            }

            Console.WriteLine("Please enter the Channel ID:");
            string channelId = Console.ReadLine()?.Trim() ?? "";

            Console.WriteLine("Please enter directories (nested directories are supported; separate multiple directories with '|'):");
            string directoriesInput = Console.ReadLine()?.Trim() ?? "";
            List<string> directories = directoriesInput
                .Split('|', StringSplitOptions.RemoveEmptyEntries)
                .Select(d => d.Trim())
                .ToList();

            Console.WriteLine("Select Max Batch Size for upload:");
            Console.WriteLine("1. 10MB");
            Console.WriteLine("2. 50MB");
            Console.WriteLine("3. 500MB");
            string option = Console.ReadLine()?.Trim() ?? "3";
            switch (option)
            {
                case "1":
                    config.MaxBatchSize = 10L * 1024 * 1024;
                    break;
                case "2":
                    config.MaxBatchSize = 50L * 1024 * 1024;
                    break;
                default:
                    config.MaxBatchSize = 500L * 1024 * 1024;
                    break;
            }

            DiscordApiClient discordClient = new DiscordApiClient(config.DiscordToken);

            List<FileItem> allFiles = FileManager.ScanFiles(directories, config.AllowedFileExtensions);
            Log.Information("Total files found after filtering: {Count}", allFiles.Count);

            var ignoredFiles = allFiles
                .Where(f => f.Size > config.MaxBatchSize)
                .Select(f => f.FileName)
                .ToList();
            List<FileItem> validFiles = allFiles
                .Where(f => f.Size <= config.MaxBatchSize)
                .ToList();

            if (!validFiles.Any())
            {
                Log.Warning("No valid files to upload.");
                await discordClient.SendSummaryMessageAsync(channelId, 0, 0, new List<string>(), ignoredFiles);
                return;
            }

            List<Batch> batches = BatchManager.CreateBatches(validFiles, config.MaxBatchSize, config.MaxFilesPerBatch);
            Log.Information("Created {Count} batch(es).", batches.Count);

            int totalSuccess = 0;
            int totalFail = 0;
            List<string> allFailedFiles = new List<string>();
            List<string> detailedReport = new List<string>();

            for (int i = 0; i < batches.Count; i++)
            {
                var result = await ProcessBatchAsync(batches[i], i, batches.Count, channelId, discordClient);
                totalSuccess += result.successCount;
                totalFail += result.failCount;
                allFailedFiles.AddRange(result.failedFiles);
                detailedReport.AddRange(result.detailedReport);
            }

            Log.Information("All batches processed. Success: {Success}, Fail: {Fail}", totalSuccess, totalFail);

            ReportManager.WriteDetailedReport("detailed_report.txt", detailedReport);

            await discordClient.SendSummaryMessageAsync(channelId, totalSuccess, totalFail, allFailedFiles, ignoredFiles);
            Log.Information("Process completed.");
        }

        private static async Task<(int successCount, int failCount, List<string> failedFiles, List<string> detailedReport)>
            ProcessBatchAsync(Batch batch, int batchIndex, int totalBatches, string channelId, DiscordApiClient discordClient)
        {
            int successCount = 0;
            int failCount = 0;
            List<string> failedFiles = new List<string>();
            List<string> detailedReport = new List<string>();
            int remainingBatches = totalBatches - batchIndex - 1;
            Log.Information("Processing Batch {BatchNumber}/{TotalBatches} (Remaining: {Remaining})", batchIndex + 1, totalBatches, remainingBatches);
            Log.Information("Files in this batch: {Count}, Total size: {Size:N2} MB", batch.Files.Count, batch.TotalSizeInMB);

            JsonDocument? uploadResponse = await discordClient.RequestUploadUrlsAsync(channelId, batch);
            if (uploadResponse == null)
            {
                Log.Error("Batch {BatchNumber}: Failed to get upload URLs.", batchIndex + 1);
                failCount += batch.Files.Count;
                failedFiles.AddRange(batch.Files.Select(f => f.FileName));
                detailedReport.Add($"Batch {batchIndex + 1}: Failed to get upload URLs.");
                return (successCount, failCount, failedFiles, detailedReport);
            }

            JsonElement attachmentsArray = uploadResponse.RootElement.GetProperty("attachments");

            var uploadTasks = batch.Files.Select(async (file, idx) =>
            {
                string uploadUrl = attachmentsArray[idx].GetProperty("upload_url").GetString() ?? "";
                Log.Information("Uploading file: {FileName} (Size: {Size:N2} MB)", file.FileName, file.ToMegabytes());
                bool uploaded = await discordClient.UploadFileAsync(uploadUrl, file);
                return (file, uploaded);
            }).ToList();

            var uploadResults = await Task.WhenAll(uploadTasks);
            int batchSuccessCount = uploadResults.Count(r => r.uploaded);
            int batchFailCount = uploadResults.Count(r => !r.uploaded);
            successCount += batchSuccessCount;
            failCount += batchFailCount;
            failedFiles.AddRange(uploadResults.Where(r => !r.uploaded).Select(r => r.file.FileName));

            if (batchFailCount > 0)
            {
                Log.Warning("Batch {BatchNumber}: Some files failed to upload. Skipping message creation.", batchIndex + 1);
                detailedReport.Add($"Batch {batchIndex + 1}: {batchSuccessCount} succeeded, {batchFailCount} failed.");
                return (successCount, failCount, failedFiles, detailedReport);
            }

            bool messageSent = await discordClient.SendMessageAsync(channelId, batch, attachmentsArray);
            if (!messageSent)
            {
                Log.Error("Batch {BatchNumber}: Failed to post message.", batchIndex + 1);
                failCount += batch.Files.Count;
                failedFiles.AddRange(batch.Files.Select(f => f.FileName));
                detailedReport.Add($"Batch {batchIndex + 1}: Failed to post message.");
            }
            else
            {
                Log.Information("Batch {BatchNumber}: Message posted successfully.", batchIndex + 1);
                detailedReport.Add($"Batch {batchIndex + 1}: {batch.Files.Count} files uploaded successfully.");
            }

            await Task.Delay(new Random().Next(5000, 10000));
            return (successCount, failCount, failedFiles, detailedReport);
        }
    }
}
