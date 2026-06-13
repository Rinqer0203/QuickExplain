using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;

namespace QuickExplain.Models
{
    public enum WindowType
    {
        SimpleResultWindow,
        MainWindow,
        Clipboard
    }

    public enum ThemeMode
    {
        System,
        Light,
        Dark
    }

    public readonly record struct WindowSize(double Width, double Height);

    public class AppConfig
    {
        public static AppConfig Instance { get; } = LoadConfig();

        public const string ConfigFileName = "appconfig.json";

        private const string LegacyDefaultSystemInstruction = "入力内容を`入力解釈ルール`に基づいて判別し、それに応じた日本語を`出力フォーマット`に従って出力してください。\r\n\r\n" +
            "```入力解釈ルール\r\n" +
            "if english:\r\n" +
            "    if 文脈を持つ文章の場合:\r\n" +
            "        日本語として自然で意味が正確に対応する文章に翻訳\r\n" +
            "    elif 単語 or 略語 or 短い語句:\r\n" +
            "        翻訳文は作らず、日本語での意味や用法を簡潔に説明\r\n" +
            "else:\r\n" +
            "    簡潔な説明\r\n" +
            "    if 難しい漢字や単語が含まれている:\r\n" +
            "        読み方や意味も簡潔に説明\r\n" +
            "```\r\n\r\n" +
            "# 出力フォーマット\r\n" +
            "- プレーンテキストのみ\r\n" +
            "- 原文の反復、判断理由、内部思考は出力しない";

        private const string DefaultSystemInstruction = "以下の入力テキストを日本語に翻訳し、その意味を簡潔に説明してください。\r\n\r\n" +
            "出力ルール:\r\n" +
            "- 必要に応じてMarkdown記法を使用する\r\n" +
            "- 原文を繰り返さない\r\n" +
            "- 判断理由や内部思考を出力しない\r\n" +
            "- 前置きや締めの文章を付けない\r\n\r\n" +
            "出力形式:\r\n" +
            "翻訳文\r\n\r\n" +
            "意味：入力テキストが表す内容を簡潔に説明する\r\n\r\n" +
            "例:\r\n" +
            "入力: Preferred 2FA method\r\n\r\n" +
            "推奨される二要素認証（2FA）の方法\r\n\r\n" +
            "意味：アカウントのログイン時に、パスワードに加えて本人確認を行うための、望ましい認証手段を指します。";

        // ここから先はJsonSerializerでシリアライズされるプロパティ
        public string GoogleApiKey { get; set; } = string.Empty;

        public string OpenAiApiKey { get; set; } = string.Empty;

        public string OllamaBaseUrl { get; set; } = "http://localhost:11434";

        public string OllamaKeepAlive { get; set; } = "5m";

        public WindowType SelectedResultWindowType { get; set; } = WindowType.MainWindow;

        public bool UseCustomInstruction { get; set; } = false;

        public AiModel[] AIModels { get; set; } = [
            new AiModel("gemini-2.5-flash-lite", AiType.Google),
            new AiModel("gemini-2.5-flash", AiType.Google),
            new AiModel("gemini-2.5-pro", AiType.Google),
            new AiModel("gemini-flash-latest", AiType.Google),
            new AiModel("gpt-5.5", AiType.OpenAi),
            new AiModel("gpt-5.4-mini", AiType.OpenAi),
            new AiModel("gpt-5.4-nano", AiType.OpenAi),
            new AiModel("gpt-4.1-mini", AiType.OpenAi),
            new AiModel("gpt-4o-mini", AiType.OpenAi),
        ];

        public AiModel SelectedAiModel { get; set; }

        public string SystemInstruction { get; set; } = DefaultSystemInstruction;

        public string CustomSystemInstruction { get; set; } = "以下の単語について説明してください\n";

        public ObservableCollection<PromptProfile> PromptProfiles { get; set; } = new();

        public string SelectedPromptId { get; set; } = string.Empty;

        public ThemeMode ThemeMode { get; set; } = ThemeMode.System;

        public WindowSize MainWindowSize { get; set; } = new WindowSize(-1, -1);

        public WindowSize SimpleResultWindowSize { get; set; } = new WindowSize(-1, -1);

        public bool MinimizeToTray { get; set; } = false;

        public bool DebugWindowPosition { get; set; } = false;

        public bool UseDummyApi { get; set; } = false;

        public bool DebugClipboardAction { get; set; } = false;

        public HotKeyDefinition GlobalHotKey { get; set; } = HotKeyDefinition.Default;

        public HotKeyDefinition ScreenshotHotKey { get; set; } = HotKeyDefinition.ScreenshotDefault;

        public bool EnableDoubleCopyAction { get; set; } = true;

        public bool ScreenshotStealthMode { get; set; } = false;

        // ここまでJsonSerializerでシリアライズされるプロパティ

        private static AppConfig LoadConfig()
        {
            AppConfig? config = null;
            if (File.Exists(ConfigFileName))
            {
                try
                {
                    string json = File.ReadAllText(ConfigFileName);
                    config = JsonSerializer.Deserialize<AppConfig>(json);
                }
                catch (JsonException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading config file '{ConfigFileName}': {ex.Message}");
                }
                catch (Exception ex) // その他の予期せぬエラー
                {
                    System.Diagnostics.Debug.WriteLine($"An unexpected error occurred while loading config: {ex.Message}");
                }
            }
            var loadedConfig = config ?? new AppConfig();
            if (loadedConfig.SystemInstruction == LegacyDefaultSystemInstruction)
                loadedConfig.SystemInstruction = DefaultSystemInstruction;

            if (loadedConfig.AIModels.Length > 0)
            {
                var selected = loadedConfig.SelectedAiModel;
                if (!string.IsNullOrWhiteSpace(selected.Name))
                {
                    var found = false;
                    foreach (var model in loadedConfig.AIModels)
                    {
                        if (model.Name == selected.Name && model.Type == selected.Type)
                        {
                            loadedConfig.SelectedAiModel = model;
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                        loadedConfig.SelectedAiModel = loadedConfig.AIModels[0];
                }
                else
                {
                    loadedConfig.SelectedAiModel = loadedConfig.AIModels[0];
                }
            }

            InitializePromptProfiles(loadedConfig);

            if (loadedConfig.GlobalHotKey.Key == Key.None || loadedConfig.GlobalHotKey.Modifiers == ModifierKeys.None)
            {
                loadedConfig.GlobalHotKey = HotKeyDefinition.Default;
            }
            else if (loadedConfig.GlobalHotKey.IsPlainCopyShortcut())
            {
                loadedConfig.GlobalHotKey = HotKeyDefinition.Default;
            }

            if (loadedConfig.ScreenshotHotKey.Key == Key.None || loadedConfig.ScreenshotHotKey.Modifiers == ModifierKeys.None)
            {
                loadedConfig.ScreenshotHotKey = HotKeyDefinition.ScreenshotDefault;
            }

            return loadedConfig;
        }

        public void SaveConfigJson()
        {
            try
            {
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigFileName, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving config file '{ConfigFileName}': {ex.Message}");
            }
        }

        public event Action<HotKeyDefinition>? GlobalHotKeyChanged;
        public event Action<HotKeyDefinition>? ScreenshotHotKeyChanged;
        public event Action<ThemeMode>? ThemeModeChanged;
        public event Action? SelectedPromptChanged;

        public void UpdateGlobalHotKey(HotKeyDefinition hotKey)
        {
            if (GlobalHotKey.Equals(hotKey))
                return;

            GlobalHotKey = hotKey;
            GlobalHotKeyChanged?.Invoke(hotKey);
        }

        public void UpdateScreenshotHotKey(HotKeyDefinition hotKey)
        {
            if (ScreenshotHotKey.Equals(hotKey))
                return;

            ScreenshotHotKey = hotKey;
            ScreenshotHotKeyChanged?.Invoke(hotKey);
        }

        public void UpdateThemeMode(ThemeMode themeMode)
        {
            if (ThemeMode == themeMode)
                return;

            ThemeMode = themeMode;
            ThemeModeChanged?.Invoke(themeMode);
        }

        public void UpdateSelectedPromptId(string promptId)
        {
            if (SelectedPromptId == promptId)
                return;

            SelectedPromptId = promptId;
            SelectedPromptChanged?.Invoke();
        }

        public PromptProfile GetSelectedPromptProfile()
        {
            if (PromptProfiles.Count == 0)
            {
                var fallback = new PromptProfile
                {
                    Name = "デフォルト",
                    Instruction = SystemInstruction
                };
                PromptProfiles.Add(fallback);
                SelectedPromptId = fallback.Id;
                return fallback;
            }

            var selected = PromptProfiles.FirstOrDefault(p => p.Id == SelectedPromptId);
            if (selected != null)
                return selected;

            SelectedPromptId = PromptProfiles[0].Id;
            return PromptProfiles[0];
        }

        private static void InitializePromptProfiles(AppConfig config)
        {
            if (config.PromptProfiles != null && config.PromptProfiles.Count > 0)
            {
                foreach (var profile in config.PromptProfiles)
                {
                    if (string.IsNullOrWhiteSpace(profile.Id))
                        profile.Id = Guid.NewGuid().ToString("N");
                    if (string.IsNullOrWhiteSpace(profile.Name))
                        profile.Name = "プロンプト";
                    if (profile.Name == "デフォルト" && profile.Instruction == LegacyDefaultSystemInstruction)
                        profile.Instruction = DefaultSystemInstruction;
                    profile.QuickQuestions ??= new ObservableCollection<QuickQuestion>();
                }
            }
            else
            {
                config.PromptProfiles = new ObservableCollection<PromptProfile>
                {
                    new PromptProfile
                    {
                        Name = "デフォルト",
                        Instruction = config.SystemInstruction
                    },
                    new PromptProfile
                    {
                        Name = "カスタム",
                        Instruction = config.CustomSystemInstruction
                    }
                };
            }

            if (string.IsNullOrWhiteSpace(config.SelectedPromptId))
            {
                if (config.UseCustomInstruction && config.PromptProfiles.Count > 1)
                {
                    config.SelectedPromptId = config.PromptProfiles[1].Id;
                }
                else
                {
                    config.SelectedPromptId = config.PromptProfiles[0].Id;
                }
            }
            else if (config.PromptProfiles.Any(p => p.Id == config.SelectedPromptId) == false)
            {
                config.SelectedPromptId = config.PromptProfiles[0].Id;
            }
        }
    }
}
