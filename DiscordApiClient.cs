using System.Net;
using System.Text;
using System.Text.Json;
using MassUploadTool.Models;
using Serilog;

namespace MassUploadTool
{
    public class DiscordApiClient
    {
        private readonly HttpClient _client;

        public DiscordApiClient(string discordToken)
        {
            _client = new HttpClient();
            _client.DefaultRequestHeaders.Add("authorization", discordToken);
        }

        public async Task<HttpResponseMessage> SendHttpRequestWithRetryAsync(
            Func<Task<HttpResponseMessage>> requestFunc,
            int maxRetries = 3,
            int delayMilliseconds = 2000)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                HttpResponseMessage response = await requestFunc();

                if (response.StatusCode == (HttpStatusCode)429)
                {
                    Log.Warning("Rate limit reached. Waiting 2 minutes before retrying.");
                    await Task.Delay(TimeSpan.FromMinutes(2));
                }
                else if (response.IsSuccessStatusCode)
                {
                    return response;
                }
                else
                {
                    Log.Warning("Attempt {Attempt} failed with status {Status}.", attempt, response.StatusCode);
                    if (attempt < maxRetries)
                        await Task.Delay(delayMilliseconds);
                    else
                        return response;
                }
            }
            throw new Exception("Unexpected error in SendHttpRequestWithRetryAsync");
        }
        public async Task<JsonDocument?> RequestUploadUrlsAsync(string channelId, Batch batch)
        {
            var uploadRequest = new
            {
                files = batch.Files.Select((file, index) => new
                {
                    filename = file.FileName,
                    file_size = file.Size,
                    id = index.ToString(),
                    is_clip = false
                }).ToArray()
            };

            string json = JsonSerializer.Serialize(uploadRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await SendHttpRequestWithRetryAsync(() =>
                _client.PostAsync($"https://discord.com/api/v9/channels/{channelId}/attachments", content));

            if (!response.IsSuccessStatusCode)
            {
                Log.Error("Failed to get upload URLs. Status: {Status}", response.StatusCode);
                return null;
            }

            string responseContent = await response.Content.ReadAsStringAsync();
            return JsonDocument.Parse(responseContent);
        }

        public async Task<bool> UploadFileAsync(string uploadUrl, FileItem file)
        {
            using (var fileStream = File.OpenRead(file.Path))
            {
                HttpResponseMessage response = await SendHttpRequestWithRetryAsync(() =>
                    _client.PutAsync(uploadUrl, new StreamContent(fileStream)));

                if (!response.IsSuccessStatusCode)
                {
                    Log.Error("Failed to upload file {FileName}. Status: {Status}", file.FileName, response.StatusCode);
                    return false;
                }
            }
            return true;
        }

        public async Task<bool> SendMessageAsync(string channelId, Batch batch, JsonElement attachmentsArray)
        {
            var messageRequest = new
            {
                content = "",
                attachments = batch.Files.Select((file, index) => new
                {
                    id = index.ToString(),
                    filename = file.FileName,
                    uploaded_filename = attachmentsArray[index].GetProperty("upload_filename").GetString()
                }).ToArray()
            };

            string json = JsonSerializer.Serialize(messageRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await SendHttpRequestWithRetryAsync(() =>
                _client.PostAsync($"https://discord.com/api/v9/channels/{channelId}/messages", content));

            if (!response.IsSuccessStatusCode)
            {
                Log.Error("Failed to send message for batch. Status: {Status}", response.StatusCode);
                return false;
            }
            return true;
        }

        public async Task<bool> SendSummaryMessageAsync(string channelId, int totalSuccess, int totalFail, List<string> failedFiles, List<string> ignoredFiles)
        {
            var summaryMessage = new
            {
                content = $"✅ **Successful uploads (PUT only)**: {totalSuccess}\n" +
                          $"❌ **Failed uploads**: {totalFail}\n" +
                          $"🚫 **Ignored files**: {ignoredFiles.Count}\n\n" +
                          (failedFiles.Any() ? $"**Failed files:**\n{string.Join("\n", failedFiles)}\n\n" : "") +
                          (ignoredFiles.Any() ? $"**Ignored files:**\n{string.Join("\n", ignoredFiles)}" : "**No failed or ignored files**")
            };

            string json = JsonSerializer.Serialize(summaryMessage);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await SendHttpRequestWithRetryAsync(() =>
                _client.PostAsync($"https://discord.com/api/v9/channels/{channelId}/messages", content));

            if (!response.IsSuccessStatusCode)
            {
                Log.Error("Failed to send summary message. Status: {Status}", response.StatusCode);
                return false;
            }
            Log.Information("Summary message sent successfully.");
            return true;
        }
    }
}
