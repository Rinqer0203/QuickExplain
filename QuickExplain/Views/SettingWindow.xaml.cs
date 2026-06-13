using QuickExplain.Models;
using QuickExplain.Services;
using MaterialDesignThemes.Wpf;
using System.Windows;
using System.Windows.Input;

namespace QuickExplain
{
    /// <summary>
    /// SettingWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class SettingWindow : Window
    {
        public SettingWindow()
        {
            InitializeComponent();
            WindowUtilities.ApplyTitleBarTheme(this);
            this.Closed += (_, _) =>
            {
                if (this.DataContext is SettingWindowViewModel vm)
                {
                    vm.OnClosed();
                }
            };
        }

        private void GlobalHotKeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (DataContext is not SettingWindowViewModel vm)
                return;

            if (!TryBuildHotKey(e, out var hotKey))
            {
                e.Handled = true;
                return;
            }

            if (hotKey.IsPlainCopyShortcut())
            {
                System.Windows.MessageBox.Show("Ctrl + C はコピー操作と競合するため、グローバルショートカットには設定できません。", "ショートカット設定", MessageBoxButton.OK, MessageBoxImage.Information);
                e.Handled = true;
                return;
            }

            vm.SetGlobalHotKey(hotKey);
            e.Handled = true;
        }

        private void ScreenshotHotKeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (DataContext is not SettingWindowViewModel vm)
                return;

            if (!TryBuildHotKey(e, out var hotKey))
            {
                e.Handled = true;
                return;
            }

            vm.SetScreenshotHotKey(hotKey);
            e.Handled = true;
        }

        private void SettingsDialogHost_DialogClosing(object sender, DialogClosingEventArgs eventArgs)
        {
            if (eventArgs.Parameter as string != "OK")
                return;

            AppConfig.Instance.ResetPromptProfiles();
            AppConfig.Instance.SaveConfigJson();
        }

        private static bool TryBuildHotKey(System.Windows.Input.KeyEventArgs e, out HotKeyDefinition hotKey)
        {
            hotKey = default;
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (IsModifierKey(key))
            {
                return false;
            }

            var modifiers = Keyboard.Modifiers;
            if (modifiers == ModifierKeys.None)
            {
                System.Windows.MessageBox.Show("修飾キー（Ctrl/Alt/Shift/Win）を含めてください。", "ショートカット設定", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }

            hotKey = new HotKeyDefinition(modifiers, key);
            return true;
        }

        private static bool IsModifierKey(Key key)
        {
            return key == Key.LeftCtrl || key == Key.RightCtrl
                || key == Key.LeftShift || key == Key.RightShift
                || key == Key.LeftAlt || key == Key.RightAlt
                || key == Key.LWin || key == Key.RWin;
        }
    }
}

