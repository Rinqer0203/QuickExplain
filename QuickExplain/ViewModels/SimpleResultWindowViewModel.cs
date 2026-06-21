using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickExplain.Models;
using QuickExplain.Services;
using QuickExplain.ViewModels;
using System.Collections.ObjectModel;

namespace QuickExplain
{
    public partial class SimpleResultWindowViewModel : ObservableObject, IProgressTextReceiver
    {
        string IProgressTextReceiver.Text
        {
            set => TranslatedText = value;
        }

        [ObservableProperty]
        private string _translatedText = string.Empty;

        [ObservableProperty]
        private string _questionText = string.Empty;

        [ObservableProperty]
        private bool _showQuickQuestions;

        [ObservableProperty]
        private bool _isRequesting;

        public ObservableCollection<QuickQuestion> QuickQuestions => AppConfig.Instance.GetSelectedPromptProfile().QuickQuestions;

        public SimpleResultWindowViewModel()
        {
            ApiRequestManager.Instance.RegisterProgressReceiver(this);
            ApiRequestManager.Instance.RequestStarted += OnRequestStarted;
            ApiRequestManager.Instance.RequestCompleted += OnRequestCompleted;
            AppConfig.Instance.SelectedPromptChanged += OnSelectedPromptChanged;
        }

        [RelayCommand]
        private async Task SendQuestion()
        {
            if (IsRequesting)
            {
                System.Media.SystemSounds.Beep.Play();
                return;
            }

            if (string.IsNullOrWhiteSpace(QuestionText))
            {
                System.Media.SystemSounds.Beep.Play();
                return;
            }

            var instance = ApiRequestManager.Instance;
            instance.AddUserMessage(QuestionText);
            QuestionText = string.Empty;
            await instance.RequestTranslation();
        }

        [RelayCommand]
        private async Task SendOrCancelQuestion()
        {
            if (IsRequesting)
            {
                CancelRequest();
                return;
            }

            await SendQuestion();
        }

        [RelayCommand]
        private void CancelRequest()
        {
            if (!IsRequesting)
            {
                System.Media.SystemSounds.Beep.Play();
                return;
            }

            ApiRequestManager.Instance.CancelCurrentRequest();
        }

        [RelayCommand]
        private async Task SendQuickQuestion(QuickQuestion? quickQuestion)
        {
            if (IsRequesting)
            {
                System.Media.SystemSounds.Beep.Play();
                return;
            }

            if (quickQuestion == null || string.IsNullOrWhiteSpace(quickQuestion.Text))
            {
                System.Media.SystemSounds.Beep.Play();
                return;
            }

            var instance = ApiRequestManager.Instance;
            instance.AddUserMessage(quickQuestion.Text);
            await instance.RequestTranslation();
        }

        private void OnSelectedPromptChanged()
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                OnPropertyChanged(nameof(QuickQuestions));
                return;
            }

            dispatcher.BeginInvoke(new Action(() => OnPropertyChanged(nameof(QuickQuestions))));
        }

        private void OnRequestStarted()
        {
            IsRequesting = true;
            ShowQuickQuestions = false;
        }

        private void OnRequestCompleted(bool success)
        {
            IsRequesting = false;
            ShowQuickQuestions = success && string.IsNullOrWhiteSpace(TranslatedText) == false;
        }
    }
}
