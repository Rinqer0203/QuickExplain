using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickExplain.Models;
using QuickExplain.Services;
using QuickExplain.ViewModels;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QuickExplain
{
    internal partial class MainWindowViewModel : ObservableObject, IProgressTextReceiver
    {
        public ObservableCollection<AiModel> AiModels { get; } = new(AppConfig.Instance.AIModels);
        public ObservableCollection<PromptProfile> PromptProfiles { get; } = AppConfig.Instance.PromptProfiles;
        public ObservableCollection<ChatMessage> ChatMessages { get; } = new();

        private ChatMessage? _streamingMessage;

        string IProgressTextReceiver.Text
        {
            set
            {
                if (_streamingMessage == null)
                    return;

                _streamingMessage.Text = value;
            }
        }

        [ObservableProperty]
        private AiModel _selectedAiModel = AppConfig.Instance.SelectedAiModel;

        [ObservableProperty]
        private PromptProfile? _selectedPromptProfile = AppConfig.Instance.GetSelectedPromptProfile();

        [ObservableProperty]
        private string _questionText = string.Empty;

        [ObservableProperty]
        private bool _showQuickQuestions;

        [ObservableProperty]
        private bool _isRequesting;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(UpdateApplicationCommand))]
        private bool _isUpdateAvailable;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(UpdateApplicationCommand))]
        private bool _isUpdatingApplication;

        [ObservableProperty]
        private string _updateButtonText = "アプリを更新";

        public MainWindowViewModel()
        {
            ApiRequestManager.Instance.RegisterProgressReceiver(this);
            ApiRequestManager.Instance.RequestStarted += OnRequestStarted;
            ApiRequestManager.Instance.RequestCompleted += OnRequestCompleted;
            AppConfig.Instance.SelectedPromptChanged += OnSelectedPromptChanged;
            AppUpdateService.Instance.UpdateAvailabilityChanged += OnUpdateAvailabilityChanged;
            _ = CheckForUpdatesIfDueAsync();
        }

        [RelayCommand]
        private async Task SendQuestion()
        {
            if (string.IsNullOrWhiteSpace(QuestionText))
            {
                System.Media.SystemSounds.Beep.Play();
                return;
            }

            var text = QuestionText;
            QuestionText = string.Empty;
            await SubmitMessageAsync(text, ChatMessages.Count == 0);
        }

        [RelayCommand]
        private async Task SendQuickQuestion(QuickQuestion? quickQuestion)
        {
            if (quickQuestion == null || string.IsNullOrWhiteSpace(quickQuestion.Text))
            {
                System.Media.SystemSounds.Beep.Play();
                return;
            }

            await SubmitMessageAsync(quickQuestion.Text, false);
        }

        [RelayCommand]
        private static void CopyMessage(ChatMessage? message)
        {
            if (message == null || string.IsNullOrWhiteSpace(message.Text))
            {
                System.Media.SystemSounds.Beep.Play();
                return;
            }

            System.Windows.Clipboard.SetText(message.Text);
        }

        [RelayCommand]
        private async Task ReloadMessage(ChatMessage? message)
        {
            if (IsRequesting || message == null || message.IsAssistant == false)
            {
                System.Media.SystemSounds.Beep.Play();
                return;
            }

            var index = ChatMessages.IndexOf(message);
            if (index <= 0 || IsImageResponse(index))
            {
                System.Media.SystemSounds.Beep.Play();
                return;
            }

            RemoveMessagesFrom(index);
            RebuildRequestMessages();

            _streamingMessage = new ChatMessage("assistant", "AI", string.Empty)
            {
                IsStreaming = true
            };
            ChatMessages.Add(_streamingMessage);

            var result = await ApiRequestManager.Instance.RequestTranslation();
            if (_streamingMessage != null)
            {
                _streamingMessage.Text = result;
                _streamingMessage.IsStreaming = false;
                _streamingMessage = null;
            }
        }

        [RelayCommand]
        private static void OpenSettingWindow()
        {
            var settingWindow = new SettingWindow();
            settingWindow.Owner = System.Windows.Application.Current.MainWindow;  // 所有者を明示
            settingWindow.ShowDialog();  // モーダル表示
        }

        [RelayCommand]
        private static void OpenPromptEditorWindow()
        {
            var promptEditorWindow = new Views.PromptEditorWindow();
            promptEditorWindow.Owner = System.Windows.Application.Current.MainWindow;
            promptEditorWindow.ShowDialog();
        }

        [RelayCommand]
        private void OpenModelAddWindow()
        {
            var viewModel = new ModelAddWindowViewModel(AiModels);
            var window = new Views.ModelAddWindow(viewModel)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            viewModel.ModelsChanged += EnsureSelectedModelIsConfigured;
            window.ShowDialog();
            viewModel.ModelsChanged -= EnsureSelectedModelIsConfigured;

            EnsureSelectedModelIsConfigured();
            AppConfig.Instance.SaveConfigJson();
        }

        private void EnsureSelectedModelIsConfigured()
        {
            if (AiModels.Contains(SelectedAiModel))
                return;

            SelectedAiModel = AiModels.FirstOrDefault();
            AppConfig.Instance.SelectedAiModel = SelectedAiModel;
        }

        [RelayCommand]
        private void StartNewChat()
        {
            ApiRequestManager.Instance.ClearMessages();
            ChatMessages.Clear();
            QuestionText = string.Empty;
            _streamingMessage = null;
            ShowQuickQuestions = false;
        }

        public async Task CheckForUpdatesIfDueAsync()
        {
            var updateFound = await AppUpdateService.Instance.CheckForUpdatesAsync();
            IsUpdateAvailable = updateFound;
        }

        private void OnUpdateAvailabilityChanged(bool isUpdateAvailable)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                IsUpdateAvailable = isUpdateAvailable;
                return;
            }

            dispatcher.Invoke(() => IsUpdateAvailable = isUpdateAvailable);
        }

        private bool CanUpdateApplication()
        {
            return IsUpdateAvailable && !IsUpdatingApplication;
        }

        [RelayCommand(CanExecute = nameof(CanUpdateApplication))]
        private async Task UpdateApplication()
        {
            IsUpdatingApplication = true;
            UpdateButtonText = "更新中...";

            try
            {
                await AppUpdateService.Instance.DownloadAndInstallUpdateAsync();
                IsUpdateAvailable = AppUpdateService.Instance.IsUpdateAvailable;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"更新に失敗しました。\n{ex.Message}",
                    "アプリの更新",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                IsUpdatingApplication = false;
                UpdateButtonText = "アプリを更新";
            }
        }

        public async Task<string> SubmitMessageAsync(string text, bool resetConversation)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var instance = ApiRequestManager.Instance;
            if (resetConversation)
            {
                instance.ClearMessages();
                ChatMessages.Clear();
            }

            ChatMessages.Add(new ChatMessage("user", "あなた", text));
            instance.AddUserMessage(text);

            _streamingMessage = new ChatMessage("assistant", "AI", string.Empty)
            {
                IsStreaming = true
            };
            ChatMessages.Add(_streamingMessage);

            var result = await instance.RequestTranslation();
            if (_streamingMessage != null)
            {
                _streamingMessage.Text = result;
                _streamingMessage.IsStreaming = false;
                _streamingMessage = null;
            }

            return result;
        }

        public async Task<string> SubmitImageMessageAsync(byte[] imageBytes, bool resetConversation)
        {
            if (imageBytes == null || imageBytes.Length == 0)
                return string.Empty;

            var instance = ApiRequestManager.Instance;
            if (resetConversation)
            {
                instance.ClearMessages();
                ChatMessages.Clear();
            }

            var imageSource = CreateImageSource(imageBytes);
            ChatMessages.Add(new ChatMessage("user", "あなた", string.Empty, imageSource));

            _streamingMessage = new ChatMessage("assistant", "AI", string.Empty)
            {
                IsStreaming = true,
                SupportsReload = false
            };
            ChatMessages.Add(_streamingMessage);

            var result = await instance.RequestImageQuestion(imageBytes);
            if (_streamingMessage != null)
            {
                _streamingMessage.Text = result;
                _streamingMessage.IsStreaming = false;
                _streamingMessage = null;
            }

            return result;
        }

        private static ImageSource? CreateImageSource(byte[] imageBytes)
        {
            try
            {
                using var stream = new MemoryStream(imageBytes);
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }

        private void RemoveMessagesFrom(int startIndex)
        {
            for (var i = ChatMessages.Count - 1; i >= startIndex; i--)
                ChatMessages.RemoveAt(i);
        }

        private void RebuildRequestMessages()
        {
            var messages = ChatMessages
                .Where(message => message.IsStreaming == false)
                .Select(message =>
                {
                    var text = message.Text;
                    if (message.IsUser && message.ImageSource != null && string.IsNullOrWhiteSpace(text))
                        text = "[画像]";

                    var role = message.IsAssistant ? "model" : "user";
                    return (role, text);
                });

            ApiRequestManager.Instance.SetMessages(messages);
        }

        private bool IsImageResponse(int assistantMessageIndex)
        {
            var previousUserMessage = ChatMessages
                .Take(assistantMessageIndex)
                .LastOrDefault(message => message.IsUser);

            return previousUserMessage?.ImageSource != null;
        }

        partial void OnSelectedAiModelChanged(AiModel value)
        {
            AppConfig.Instance.SelectedAiModel = value;
        }

        partial void OnSelectedPromptProfileChanged(PromptProfile? value)
        {
            if (value == null)
                return;

            AppConfig.Instance.UpdateSelectedPromptId(value.Id);
        }

        private void OnRequestStarted()
        {
            IsRequesting = true;
            ShowQuickQuestions = false;
        }

        private void OnRequestCompleted(bool success)
        {
            IsRequesting = false;
            ShowQuickQuestions = success
                && ChatMessages.Any(message =>
                    message.Role == "assistant" &&
                    string.IsNullOrWhiteSpace(message.Text) == false);
        }

        private void OnSelectedPromptChanged()
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                SelectedPromptProfile = AppConfig.Instance.GetSelectedPromptProfile();
                return;
            }

            dispatcher.BeginInvoke(new Action(() => SelectedPromptProfile = AppConfig.Instance.GetSelectedPromptProfile()));
        }
    }
}
