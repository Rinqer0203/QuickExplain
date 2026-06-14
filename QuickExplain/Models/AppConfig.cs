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

        public static string ConfigFilePath => Path.Combine(AppContext.BaseDirectory, ConfigFileName);

        private const string DefaultSystemInstruction = "以下の入力テキストを日本語に翻訳し、その意味を簡潔に説明してください。\r\n\r\n" +
            "ユーザーから追加質問された場合は、質問に対して簡潔に答えてください。\r\n\r\n" +
            "追加質問への回答ルール:\r\n" +
            "- 入力が英語の場合は、元の英語表現について答える\r\n" +
            "- 入力が英語の場合、読み方は英語としての発音をカタカナで答える\r\n" +
            "- 入力が英語の場合、例文は英語の自然な例文を作る\r\n" +
            "- 入力が英語の場合、使い分けは英語の類似表現との違いを説明する\r\n" +
            "- 入力が日本語の場合だけ、日本語の読み・日本語の例文・日本語表現の使い分けを答える\r\n\r\n" +
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

        private const string DefaultCustomSystemInstruction = "以下の単語について説明してください\n";

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

        public string CustomSystemInstruction { get; set; } = DefaultCustomSystemInstruction;

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
            if (File.Exists(ConfigFilePath))
            {
                try
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    config = JsonSerializer.Deserialize<AppConfig>(json);
                }
                catch (JsonException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading config file '{ConfigFilePath}': {ex.Message}");
                }
                catch (Exception ex) // その他の予期せぬエラー
                {
                    System.Diagnostics.Debug.WriteLine($"An unexpected error occurred while loading config: {ex.Message}");
                }
            }
            var loadedConfig = config ?? new AppConfig();

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
                File.WriteAllText(ConfigFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving config file '{ConfigFilePath}': {ex.Message}");
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

        public void ResetPromptProfiles()
        {
            SystemInstruction = DefaultSystemInstruction;
            CustomSystemInstruction = DefaultCustomSystemInstruction;
            UseCustomInstruction = false;

            PromptProfiles.Clear();
            foreach (var profile in CreateDefaultPromptProfiles())
                PromptProfiles.Add(profile);

            SelectedPromptId = PromptProfiles[0].Id;
            SelectedPromptChanged?.Invoke();
        }

        public PromptProfile GetSelectedPromptProfile()
        {
            if (PromptProfiles.Count == 0)
            {
                var fallback = new PromptProfile
                {
                    Name = "デフォルト",
                    Instruction = SystemInstruction,
                    QuickQuestions = CreateDefaultQuickQuestions()
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
                    profile.QuickQuestions ??= new ObservableCollection<QuickQuestion>();
                }
            }
            else
            {
                config.PromptProfiles = CreateDefaultPromptProfiles(config.SystemInstruction, config.CustomSystemInstruction);
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

        private static ObservableCollection<PromptProfile> CreateDefaultPromptProfiles(
            string? defaultInstruction = null,
            string? customInstruction = null) =>
        [
            new PromptProfile
            {
                Name = "デフォルト",
                Instruction = defaultInstruction ?? DefaultSystemInstruction,
                QuickQuestions = CreateDefaultQuickQuestions()
            },
            new PromptProfile
            {
                Name = "カスタム",
                Instruction = customInstruction ?? DefaultCustomSystemInstruction
            }
        ];

        private static ObservableCollection<QuickQuestion> CreateDefaultQuickQuestions() =>
        [
            new QuickQuestion
            {
                Title = "読み方は？",
                Text = "この入力の読み方を教えてください。英語の場合は英語としての発音をカタカナで答えてください。"
            },
            new QuickQuestion
            {
                Title = "例文を作って",
                Text = "自然な例文を3つ作ってください。英語入力の場合は英語の例文を作ってください。"
            },
            new QuickQuestion
            {
                Title = "使い分けは？",
                Text = "似た表現との違いや使い分けを簡潔に教えてください。英語入力の場合は英語の類似表現と比較してください。"
            }
        ];
    }
}
