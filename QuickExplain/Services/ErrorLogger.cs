using System.IO;
using System.Text;
using System.Runtime.InteropServices;

namespace QuickExplain.Services
{
    public static class ErrorLogger
    {
        public const string LogFileName = "error.log";

        public static string LogFilePath => AppPathService.GetApplicationFilePath(LogFileName);

        public static void Log(string type, Exception? ex)
        {
            Log(type, (object?)ex);
        }

        public static void Log(string type, object? details)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] {type}");
            sb.AppendLine($"Version: {typeof(ErrorLogger).Assembly.GetName().Version?.ToString() ?? "unknown"}");
            sb.AppendLine($"OS: {RuntimeInformation.OSDescription}");
            sb.AppendLine($".NET: {RuntimeInformation.FrameworkDescription}");
            sb.AppendLine("Details:");
            sb.AppendLine(details switch
            {
                null => "例外情報が null です。",
                Exception ex => ex.ToString(),
                _ => details.ToString() ?? "例外情報を文字列化できませんでした。"
            });
            sb.AppendLine(new string('-', 80));

            try
            {
                File.AppendAllText(LogFilePath, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }
    }
}
