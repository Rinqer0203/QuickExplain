using System.IO;

namespace QuickExplain.Services
{
    public static class AppPathService
    {
        public static string ApplicationDirectory
        {
            get
            {
                var processPath = Environment.ProcessPath;
                var processDirectory = string.IsNullOrWhiteSpace(processPath)
                    ? null
                    : Path.GetDirectoryName(processPath);

                return Path.GetFullPath(processDirectory ?? AppContext.BaseDirectory);
            }
        }

        public static string GetApplicationFilePath(string fileName)
        {
            return Path.Combine(ApplicationDirectory, fileName);
        }

        public static void SetCurrentDirectoryToApplicationDirectory()
        {
            try
            {
                Environment.CurrentDirectory = ApplicationDirectory;
            }
            catch
            {
            }
        }
    }
}
