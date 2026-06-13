using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Serialization;

namespace QuickExplain.Models
{
    public partial class QuickQuestion : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayText))]
        private string _title = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayText))]
        private string _text = string.Empty;

        [JsonIgnore]
        public string DisplayText
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Title))
                    return Title;

                if (!string.IsNullOrWhiteSpace(Text))
                    return Text;

                return "クイック質問";
            }
        }
    }
}
