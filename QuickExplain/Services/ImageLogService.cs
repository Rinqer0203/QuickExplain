using System.Globalization;
using System.IO;

namespace QuickExplain.Services
{
    public static class ImageLogService
    {
        private const string LogDirectoryName = "log";

        public static string SaveSentImage(byte[] imageBytes, string aiProvider)
        {
            var logDirectory = Path.Combine(AppPathService.ApplicationDirectory, LogDirectoryName);
            Directory.CreateDirectory(logDirectory);

            var safeProviderName = string.Concat(aiProvider.Select(c => char.IsLetterOrDigit(c) ? c : '_'));
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
            var filePath = Path.Combine(logDirectory, $"{timestamp}_{safeProviderName}.png");
            File.WriteAllBytes(filePath, imageBytes);
            return filePath;
        }
    }
}
