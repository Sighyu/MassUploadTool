using System.Text.Json;
using MassUploadTool.Models;
using Serilog;

namespace MassUploadTool.Managers
{
    public static class ConfigManager
    {
        public static AppConfig LoadConfiguration(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return new AppConfig();
                string json = File.ReadAllText(path);
                var config = JsonSerializer.Deserialize<AppConfig>(json);
                return config ?? new AppConfig();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading configuration from {Path}", path);
                return new AppConfig();
            }
        }

        public static void SaveConfiguration(string path, AppConfig config)
        {
            try
            {
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
                Log.Information("Configuration saved to {Path}", path);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error saving configuration to {Path}", path);
            }
        }
    }
}
