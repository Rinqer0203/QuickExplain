using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickExplain.Models;
using QuickExplain.Services;
using IWshRuntimeLibrary;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;

namespace QuickExplain
{
    public partial class SettingWindowViewModel : ObservableObject
    {
        public List<WindowType> WindowTypeItems { get; }
        public List<ThemeModeItem> ThemeModeItems { get; }
        public List<OllamaKeepAliveItem> OllamaKeepAliveItems { get; }

        [ObservableProperty]
        private string _googleApiKey = AppConfig.Instance.GoogleApiKey;

        [ObservableProperty]
        private string _openAiApiKey = AppConfig.Instance.OpenAiApiKey;

        [ObservableProperty]
        private string _ollamaBaseUrl = AppConfig.Instance.OllamaBaseUrl;

        [ObservableProperty]
        private string _selectedOllamaKeepAlive = AppConfig.Instance.OllamaKeepAlive;

        [ObservableProperty]
        private WindowType _selectedResultWindowType;

        [ObservableProperty]
        private ThemeMode _selectedThemeMode;

        [ObservableProperty]
        private bool _startupWithWindows = StartupShortcutExists();

        [ObservableProperty]
        private bool _minimizeToTray = AppConfig.Instance.MinimizeToTray;

        [ObservableProperty]
        private bool _enableDoubleCopyAction = AppConfig.Instance.EnableDoubleCopyAction;

        [ObservableProperty]
        private HotKeyDefinition _globalHotKey = AppConfig.Instance.GlobalHotKey;

        [ObservableProperty]
        private string _globalHotKeyDisplay = string.Empty;

        [ObservableProperty]
        private HotKeyDefinition _screenshotHotKey = AppConfig.Instance.ScreenshotHotKey;

        [ObservableProperty]
        private string _screenshotHotKeyDisplay = string.Empty;

        [ObservableProperty]
        private string _updateStatusText = "更新を確認中です";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CheckForUpdatesCommand))]
        private bool _isCheckingUpdate;

        public string AppVersionText => $"バージョン {AppUpdateService.Instance.CurrentVersion}";

        public SettingWindowViewModel()
        {
            WindowTypeItems = Enum.GetValues(typeof(WindowType))
                .Cast<WindowType>()
                .Where(type => type != WindowType.Clipboard)
                .ToList();
            ThemeModeItems =
            [
                new ThemeModeItem(ThemeMode.System, "システム"),
                new ThemeModeItem(ThemeMode.Light, "ライト"),
                new ThemeModeItem(ThemeMode.Dark, "ダーク")
            ];
            OllamaKeepAliveItems =
            [
                new OllamaKeepAliveItem("0", "リクエスト後すぐ解放"),
                new OllamaKeepAliveItem("1m", "短め: 1分"),
                new OllamaKeepAliveItem("5m", "標準: 5分"),
                new OllamaKeepAliveItem("30m", "長め: 30分"),
                new OllamaKeepAliveItem("-1m", "常時保持")
            ];
            SelectedResultWindowType = AppConfig.Instance.SelectedResultWindowType;
            SelectedThemeMode = AppConfig.Instance.ThemeMode;
            if (SelectedOllamaKeepAlive == "-1")
                SelectedOllamaKeepAlive = "-1m";

            if (OllamaKeepAliveItems.Any(item => item.Value == SelectedOllamaKeepAlive) == false)
                SelectedOllamaKeepAlive = "5m";

            GlobalHotKeyDisplay = FormatHotKey(GlobalHotKey);
            ScreenshotHotKeyDisplay = FormatHotKey(ScreenshotHotKey);
            _ = CheckUpdateStatusAsync(force: false);
        }

        public void OnClosed()
        {
            AppConfig.Instance.SaveConfigJson();
        }

        partial void OnGoogleApiKeyChanged(string value)
        {
            AppConfig.Instance.GoogleApiKey = value;
        }

        partial void OnOpenAiApiKeyChanged(string value)
        {
            AppConfig.Instance.OpenAiApiKey = value;
        }

        partial void OnOllamaBaseUrlChanged(string value)
        {
            AppConfig.Instance.OllamaBaseUrl = string.IsNullOrWhiteSpace(value)
                ? "http://localhost:11434"
                : value.Trim();
        }

        partial void OnSelectedOllamaKeepAliveChanged(string value)
        {
            AppConfig.Instance.OllamaKeepAlive = string.IsNullOrWhiteSpace(value)
                ? "5m"
                : value;
        }

        partial void OnSelectedResultWindowTypeChanged(WindowType value)
        {
            AppConfig.Instance.SelectedResultWindowType = value;
        }

        partial void OnSelectedThemeModeChanged(ThemeMode value)
        {
            AppConfig.Instance.UpdateThemeMode(value);
        }

        partial void OnStartupWithWindowsChanged(bool value)
        {
            string appName = "QuickExplain";
            string startupFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            string shortcutPath = Path.Combine(startupFolderPath, $"{appName}.lnk");

            // 実行ファイル(.exe)のフルパスを取得
            var mainModule = Process.GetCurrentProcess().MainModule
                 ?? throw new InvalidOperationException("現在のプロセスのメインモジュールが取得できませんでした。");
            string exePath = mainModule.FileName;

            if (value)
            {
                // スタートアップにショートカットを作成する
                var shell = new WshShell();
                IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = exePath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(exePath);
                shortcut.Description = "QuickExplain 自動起動";
                shortcut.Save();
            }
            else
            {
                // スタートアップからショートカットを削除する
                if (System.IO.File.Exists(shortcutPath))
                {
                    System.IO.File.Delete(shortcutPath);
                }
            }

            var exists = StartupShortcutExists();
            if (_startupWithWindows != exists)
            {
                _startupWithWindows = exists;
                OnPropertyChanged(nameof(StartupWithWindows));
            }
        }

        partial void OnMinimizeToTrayChanged(bool value)
        {
            AppConfig.Instance.MinimizeToTray = value;
        }

        partial void OnEnableDoubleCopyActionChanged(bool value)
        {
            AppConfig.Instance.EnableDoubleCopyAction = value;
        }

        partial void OnGlobalHotKeyChanged(HotKeyDefinition value)
        {
            AppConfig.Instance.UpdateGlobalHotKey(value);
            GlobalHotKeyDisplay = FormatHotKey(value);
        }

        partial void OnScreenshotHotKeyChanged(HotKeyDefinition value)
        {
            AppConfig.Instance.UpdateScreenshotHotKey(value);
            ScreenshotHotKeyDisplay = FormatHotKey(value);
        }

        public void SetGlobalHotKey(HotKeyDefinition hotKey)
        {
            GlobalHotKey = hotKey;
        }

        public void SetScreenshotHotKey(HotKeyDefinition hotKey)
        {
            ScreenshotHotKey = hotKey;
        }

        private bool CanCheckForUpdates()
        {
            return !IsCheckingUpdate;
        }

        [RelayCommand(CanExecute = nameof(CanCheckForUpdates))]
        private async Task CheckForUpdates()
        {
            await CheckUpdateStatusAsync(force: true);
        }

        private async Task CheckUpdateStatusAsync(bool force)
        {
            if (!AppUpdateService.CanUseUpdater)
            {
                UpdateStatusText = "デバッグビルドでは更新を確認しません";
                return;
            }

            IsCheckingUpdate = true;
            UpdateStatusText = "更新を確認中です";

            try
            {
                var updateFound = await AppUpdateService.Instance.CheckForUpdatesAsync(force: force);
                if (updateFound)
                {
                    var latestVersion = AppUpdateService.Instance.LatestVersion;
                    UpdateStatusText = string.IsNullOrWhiteSpace(latestVersion)
                        ? "新しいバージョンがあります"
                        : $"新しいバージョンがあります ({latestVersion})";
                    return;
                }

                UpdateStatusText = "最新の状態です";
            }
            finally
            {
                IsCheckingUpdate = false;
            }
        }

        private static string FormatHotKey(HotKeyDefinition hotKey)
        {
            var parts = new List<string>();
            if ((hotKey.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) parts.Add("Ctrl");
            if ((hotKey.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) parts.Add("Shift");
            if ((hotKey.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) parts.Add("Alt");
            if ((hotKey.Modifiers & ModifierKeys.Windows) == ModifierKeys.Windows) parts.Add("Win");
            parts.Add(hotKey.Key.ToString());
            return string.Join(" + ", parts);
        }

        private static bool StartupShortcutExists()
        {
            string appName = "QuickExplain";
            string startupFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            string shortcutPath = Path.Combine(startupFolderPath, $"{appName}.lnk");
            return System.IO.File.Exists(shortcutPath);
        }

        public readonly record struct ThemeModeItem(ThemeMode Mode, string Label);

        public readonly record struct OllamaKeepAliveItem(string Value, string Label);
    }
}
