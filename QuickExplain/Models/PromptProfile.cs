using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace QuickExplain.Models
{
    public partial class PromptProfile : ObservableObject
    {
        [ObservableProperty]
        private string _id = Guid.NewGuid().ToString("N");

        [ObservableProperty]
        private string _name = "デフォルト";

        [ObservableProperty]
        private string _instruction = string.Empty;

        public ObservableCollection<QuickQuestion> QuickQuestions { get; set; } = new();
    }
}
