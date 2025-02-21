using Serilog;

namespace MassUploadTool.Managers
{
    public static class ReportManager
    {
        public static void WriteDetailedReport(string path, List<string> reportLines)
        {
            try
            {
                File.WriteAllLines(path, reportLines);
                Log.Information("Detailed report written to {Path}", path);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error writing detailed report to {Path}", path);
            }
        }
    }
}
