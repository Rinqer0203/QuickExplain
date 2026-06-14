using IWshRuntimeLibrary;
using System.Diagnostics;
using System.IO;

namespace QuickExplain.Services
{
    internal static class StartupShortcutService
    {
        private const string AppName = "QuickExplain";

        private static string ShortcutPath
        {
            get
            {
                var startupFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                return Path.Combine(startupFolderPath, $"{AppName}.lnk");
            }
        }

        public static bool Exists()
        {
            return System.IO.File.Exists(ShortcutPath);
        }

        public static void CreateOrUpdate()
        {
            var exePath = GetCurrentExecutablePath();
            var shell = new WshShell();
            IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(ShortcutPath);
            shortcut.TargetPath = exePath;
            shortcut.WorkingDirectory = Path.GetDirectoryName(exePath);
            shortcut.Description = "QuickExplain 自動起動";
            shortcut.Save();
        }

        public static void Delete()
        {
            if (System.IO.File.Exists(ShortcutPath))
            {
                System.IO.File.Delete(ShortcutPath);
            }
        }

        public static void RepairIfExists()
        {
            if (!Exists())
                return;

            try
            {
                if (!IsShortcutTargetCurrentExecutable())
                {
                    CreateOrUpdate();
                }
            }
            catch
            {
                // 起動時の自動修復に失敗しても、アプリ起動自体は継続する。
            }
        }

        private static bool IsShortcutTargetCurrentExecutable()
        {
            var shell = new WshShell();
            IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(ShortcutPath);
            var targetPath = shortcut.TargetPath;
            if (string.IsNullOrWhiteSpace(targetPath) || !System.IO.File.Exists(targetPath))
                return false;

            return PathsEqual(targetPath, GetCurrentExecutablePath());
        }

        private static string GetCurrentExecutablePath()
        {
            if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
                return Environment.ProcessPath;

            var mainModule = Process.GetCurrentProcess().MainModule
                ?? throw new InvalidOperationException("現在のプロセスのメインモジュールが取得できませんでした。");
            return mainModule.FileName;
        }

        private static bool PathsEqual(string left, string right)
        {
            return string.Equals(
                Path.GetFullPath(left),
                Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
