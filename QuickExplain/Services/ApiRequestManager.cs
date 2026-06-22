using QuickExplain.Models;
using QuickExplain.Models.Extensions;
using QuickExplain.Services.AiProviders;
using QuickExplain.Services.ApiClients;
using QuickExplain.ViewModels;
using System;
using System.Net.Http;
using System.Text;
using System.Windows.Threading;

namespace QuickExplain.Services
{
    /// <summary>
    /// Google APIリクエストを管理して、<see cref="RegisterProgressReceiver"/>で登録されたReceiverに進捗を通知するクラス
    /// </summary>
    public class ApiRequestManager
    {
        public static ApiRequestManager Instance { get; } = new ApiRequestManager();

        private readonly AiProviderRegistry _providerRegistry;
        private readonly StringBuilder _sb = new();
        private readonly List<(string role, string text)> _messages = new(64);
        private readonly List<IProgressTextReceiver> _progressReceivers = new();
        private readonly object _progressUpdateLock = new();
        private string? _pendingProgressText;
        private DispatcherTimer? _progressUpdateTimer;
        private bool _isProgressUpdateTimerActive;
        private CancellationTokenSource? _requestCancellationTokenSource;
        public TokenUsage? LastTokenUsage { get; private set; }
        public event Action? RequestStarted;
        public event Action<bool>? RequestCompleted;
        private bool _requestHadError;
        private static readonly TimeSpan ProgressUpdateInterval = TimeSpan.FromMilliseconds(80);

        private ApiRequestManager()
        {
            var httpClient = new HttpClient();

            if (AppConfig.Instance.UseDummyApi)
            {
                _providerRegistry = new AiProviderRegistry(
                [
                    new GoogleProvider(new DummyGoogleApiClient()),
                    new OpenAiProvider(new DummyOpenAiApiClient()),
                    new DummyOllamaProvider()
                ]);
            }
            else
            {
                _providerRegistry = new AiProviderRegistry(
                [
                    new GoogleProvider(new GoogleApiClient(httpClient)),
                    new OpenAiProvider(new OpenAiApiClient(httpClient)),
                    new OllamaProvider(httpClient)
                ]);
            }

        }

        private bool _isRequesting = false;

        public void CancelCurrentRequest()
        {
            _requestCancellationTokenSource?.Cancel();
        }

        public void AddUserMessage(string text)
        {
            _messages.Add(("user", text));
        }

        public void RegisterProgressReceiver(IProgressTextReceiver receiver)
        {
            if (!_progressReceivers.Contains(receiver))
            {
                _progressReceivers.Add(receiver);
            }
        }

        public void UnregisterProgressReceiver(IProgressTextReceiver receiver)
        {
            _progressReceivers.Remove(receiver);
        }

        public void ClearMessages()
        {
            _messages.Clear();
        }

        public void SetMessages(IEnumerable<(string role, string text)> messages)
        {
            _messages.Clear();
            foreach (var (role, text) in messages)
            {
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                _messages.Add((role, text));
            }
        }

        public async Task<string> RequestTranslation()
        {
            return (await RequestTranslationResult()).Text;
        }

        public async Task<AiRequestResult> RequestTranslationResult()
        {
            if (_isRequesting)
            {
                System.Media.SystemSounds.Beep.Play();
                return new AiRequestResult("リクエスト中です", null, false, false);
            }

            _isRequesting = true;
            using var cancellationTokenSource = new CancellationTokenSource();
            _requestCancellationTokenSource = cancellationTokenSource;
            var cancellationToken = cancellationTokenSource.Token;
            RequestStarted?.Invoke();

            var success = false;
            var canceled = false;
            try
            {
                _sb.Clear();
                LastTokenUsage = null;
                _requestHadError = false;
                var config = AppConfig.Instance;
                if (string.IsNullOrWhiteSpace(config.SelectedAiModel.Name))
                {
                    _requestHadError = true;
                    return new AiRequestResult("使用するAIモデルが設定されていません。モデル編集からモデルを追加してください。", null, false, false);
                }

                if (!_providerRegistry.TryGetProvider(config.SelectedAiModel.Type, out var provider))
                {
                    _requestHadError = true;
                    return new AiRequestResult("サポートされていないAIモデルです", null, false, false);
                }

                var request = new AiProviderRequest(
                    config.SelectedAiModel.Name,
                    GetSystemInstruction(),
                    _messages.ToArray());
                OnStatusAction("応答を待っています...");
                await provider.StreamGenerateContentAsync(request, OnGetContentAction, OnStatusAction, OnErrorAction, OnTokenUsageAction, cancellationToken);

                var result = _sb.ToString();
                FlushProgressReceivers(result);

                // システムの返答のロールをmodelにしているが、
                // それぞれのCreateRequestで決められたAPIロールに変換されるので問題ない
                _messages.Add(("model", result));

                success = !_requestHadError;
                return new AiRequestResult(result, LastTokenUsage, success, false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                canceled = true;
                _requestHadError = true;
                var result = _sb.ToString();
                if (string.IsNullOrWhiteSpace(result))
                    result = "キャンセルされました";
                else
                    result = $"{result}{Environment.NewLine}{Environment.NewLine}(キャンセルされました)";

                FlushProgressReceivers(result);
                return new AiRequestResult(result, null, false, true);
            }
            finally
            {
                if (ReferenceEquals(_requestCancellationTokenSource, cancellationTokenSource))
                    _requestCancellationTokenSource = null;

                _isRequesting = false;
                RequestCompleted?.Invoke(success && !canceled);
            }
        }

        public async Task<string> RequestImageQuestion(byte[] imageBytes)
        {
            return (await RequestImageQuestionResult(imageBytes)).Text;
        }

        public async Task<AiRequestResult> RequestImageQuestionResult(byte[] imageBytes)
        {
            if (_isRequesting)
            {
                System.Media.SystemSounds.Beep.Play();
                return new AiRequestResult("リクエスト中です", null, false, false);
            }

            if (imageBytes == null || imageBytes.Length == 0)
                return new AiRequestResult("画像データが空です", null, false, false);

            _isRequesting = true;
            using var cancellationTokenSource = new CancellationTokenSource();
            _requestCancellationTokenSource = cancellationTokenSource;
            var cancellationToken = cancellationTokenSource.Token;
            RequestStarted?.Invoke();

            var success = false;
            var canceled = false;
            try
            {
                _sb.Clear();
                LastTokenUsage = null;
                _requestHadError = false;

                var config = AppConfig.Instance;
                if (config.SaveScreenshots)
                    ImageLogService.SaveSentImage(imageBytes, config.SelectedAiModel.Type.ToString());

                if (string.IsNullOrWhiteSpace(config.SelectedAiModel.Name))
                {
                    _requestHadError = true;
                    return new AiRequestResult("使用するAIモデルが設定されていません。モデル編集からモデルを追加してください。", null, false, false);
                }

                if (!_providerRegistry.TryGetProvider(config.SelectedAiModel.Type, out var provider))
                {
                    _requestHadError = true;
                    return new AiRequestResult("サポートされていないAIモデルです", null, false, false);
                }

                var request = new AiProviderRequest(
                    config.SelectedAiModel.Name,
                    GetSystemInstruction(),
                    _messages.ToArray(),
                    imageBytes);
                OnStatusAction("応答を待っています...");
                await provider.StreamGenerateContentAsync(request, OnGetContentAction, OnStatusAction, OnErrorAction, OnTokenUsageAction, cancellationToken);

                var result = _sb.ToString();
                FlushProgressReceivers(result);
                _messages.Add(("user", "[画像]"));
                _messages.Add(("model", result));

                success = !_requestHadError;
                return new AiRequestResult(result, LastTokenUsage, success, false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                canceled = true;
                _requestHadError = true;
                var result = _sb.ToString();
                if (string.IsNullOrWhiteSpace(result))
                    result = "キャンセルされました";
                else
                    result = $"{result}{Environment.NewLine}{Environment.NewLine}(キャンセルされました)";

                FlushProgressReceivers(result);
                return new AiRequestResult(result, null, false, true);
            }
            finally
            {
                if (ReferenceEquals(_requestCancellationTokenSource, cancellationTokenSource))
                    _requestCancellationTokenSource = null;

                _isRequesting = false;
                RequestCompleted?.Invoke(success && !canceled);
            }
        }

        private void OnGetContentAction(string text)
        {
            _sb.Append(text);
            var currentText = _sb.ToString();
            UpdateProgressReceivers(currentText);
        }

        private void OnStatusAction(string text)
        {
            var currentText = _sb.Length == 0
                ? text
                : $"{_sb}{Environment.NewLine}{text}";
            UpdateProgressReceivers(currentText);
        }

        private void OnErrorAction(string text)
        {
            _requestHadError = true;
            _sb.Append(text);
            var currentText = _sb.ToString();
            UpdateProgressReceivers(currentText);
        }

        private void OnTokenUsageAction(TokenUsage usage)
        {
            LastTokenUsage = usage;
        }

        private void UpdateProgressReceivers(string text)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                SetProgressReceiverText(text);
                return;
            }

            lock (_progressUpdateLock)
            {
                _pendingProgressText = text;
                if (_isProgressUpdateTimerActive)
                    return;

                _isProgressUpdateTimerActive = true;
            }

            dispatcher.BeginInvoke(EnsureProgressUpdateTimer, DispatcherPriority.Background);
        }

        private void EnsureProgressUpdateTimer()
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                PublishPendingProgressText();
                return;
            }

            if (_progressUpdateTimer == null)
            {
                _progressUpdateTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
                {
                    Interval = ProgressUpdateInterval
                };
                _progressUpdateTimer.Tick += (_, _) => PublishPendingProgressText();
            }

            if (!_progressUpdateTimer.IsEnabled)
                _progressUpdateTimer.Start();
        }

        private void PublishPendingProgressText()
        {
            string? text;
            lock (_progressUpdateLock)
            {
                text = _pendingProgressText;
                _pendingProgressText = null;

                if (text == null)
                {
                    _isProgressUpdateTimerActive = false;
                    _progressUpdateTimer?.Stop();
                    return;
                }
            }

            SetProgressReceiverText(text);
        }

        private void FlushProgressReceivers(string text)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                FlushProgressReceiversOnDispatcher(text);
                return;
            }

            dispatcher.Invoke(() => FlushProgressReceiversOnDispatcher(text), DispatcherPriority.Send);
        }

        private void FlushProgressReceiversOnDispatcher(string text)
        {
            lock (_progressUpdateLock)
            {
                _pendingProgressText = null;
                _isProgressUpdateTimerActive = false;
                _progressUpdateTimer?.Stop();
            }

            SetProgressReceiverText(text);
        }

        private void SetProgressReceiverText(string text)
        {
            foreach (var holder in _progressReceivers.ToArray())
            {
                holder.Text = text;
            }
        }

        private static string GetSystemInstruction()
        {
            return AppConfig.Instance.GetSelectedPromptProfile().Instruction;
        }
    }
}
