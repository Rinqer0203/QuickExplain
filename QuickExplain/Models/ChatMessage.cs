using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;

namespace QuickExplain.Models
{
    public partial class ChatMessage : ObservableObject
    {
        public string Role { get; }
        public string DisplayName { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanCopyText))]
        [NotifyPropertyChangedFor(nameof(CanShowUserActions))]
        private string _text;

        [ObservableProperty]
        private ImageSource? _imageSource;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanCopyText))]
        [NotifyPropertyChangedFor(nameof(CanReload))]
        [NotifyPropertyChangedFor(nameof(CanShowAssistantActions))]
        [NotifyPropertyChangedFor(nameof(CanShowUserActions))]
        [NotifyPropertyChangedFor(nameof(CanShowTokenUsage))]
        private bool _isStreaming;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanReload))]
        private bool _supportsReload = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanShowTokenUsage))]
        private string _tokenUsageText = string.Empty;

        public bool IsUser => Role == "user";

        public bool IsAssistant => Role == "assistant";

        public bool CanCopyText => !IsStreaming && string.IsNullOrWhiteSpace(Text) == false;

        public bool CanShowUserActions => IsUser && CanCopyText;

        public bool CanShowAssistantActions => IsAssistant && !IsStreaming;

        public bool CanReload => IsAssistant && SupportsReload && !IsStreaming;

        public bool CanShowTokenUsage => IsAssistant && !IsStreaming && string.IsNullOrWhiteSpace(TokenUsageText) == false;

        public ChatMessage(string role, string displayName, string text, ImageSource? imageSource = null)
        {
            Role = role;
            DisplayName = displayName;
            _text = text;
            _imageSource = imageSource;
        }
    }
}
