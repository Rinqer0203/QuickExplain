using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickExplain.Models;
using System.Collections.ObjectModel;

namespace QuickExplain.ViewModels
{
    public partial class PromptEditorViewModel : ObservableObject
    {
        public ObservableCollection<PromptProfile> PromptProfiles { get; } = AppConfig.Instance.PromptProfiles;

        [ObservableProperty]
        private PromptProfile? _selectedPromptProfile;

        [ObservableProperty]
        private string _promptName = string.Empty;

        [ObservableProperty]
        private string _promptInstruction = string.Empty;

        public PromptEditorViewModel()
        {
            SelectedPromptProfile = AppConfig.Instance.GetSelectedPromptProfile();
            if (SelectedPromptProfile != null)
            {
                PromptName = SelectedPromptProfile.Name;
                PromptInstruction = SelectedPromptProfile.Instruction;
            }
        }

        public void OnClosed()
        {
            AppConfig.Instance.SaveConfigJson();
        }

        partial void OnSelectedPromptProfileChanged(PromptProfile? value)
        {
            if (value == null)
                return;

            AppConfig.Instance.UpdateSelectedPromptId(value.Id);
            PromptName = value.Name;
            PromptInstruction = value.Instruction;
        }

        partial void OnPromptNameChanged(string value)
        {
            if (SelectedPromptProfile == null)
                return;

            SelectedPromptProfile.Name = value;
        }

        partial void OnPromptInstructionChanged(string value)
        {
            if (SelectedPromptProfile == null)
                return;

            SelectedPromptProfile.Instruction = value;
        }

        [RelayCommand]
        private void AddPromptProfile()
        {
            var profile = new PromptProfile
            {
                Name = "新しいプロンプト",
                Instruction = string.Empty
            };
            PromptProfiles.Add(profile);
            SelectedPromptProfile = profile;
        }

        [RelayCommand]
        private void RemovePromptProfile()
        {
            if (SelectedPromptProfile == null || PromptProfiles.Count <= 1)
                return;

            var index = PromptProfiles.IndexOf(SelectedPromptProfile);
            PromptProfiles.Remove(SelectedPromptProfile);

            if (PromptProfiles.Count == 0)
                return;

            if (index >= PromptProfiles.Count)
                index = PromptProfiles.Count - 1;

            SelectedPromptProfile = PromptProfiles[index];
        }

        [RelayCommand]
        private void AddQuickQuestion()
        {
            if (SelectedPromptProfile == null)
                return;

            var quickQuestion = new QuickQuestion
            {
                Title = "新しい質問",
                Text = "新しい質問"
            };
            SelectedPromptProfile.QuickQuestions.Add(quickQuestion);
        }

        [RelayCommand]
        private void RemoveQuickQuestion(QuickQuestion? quickQuestion)
        {
            if (SelectedPromptProfile == null || quickQuestion == null)
                return;

            SelectedPromptProfile.QuickQuestions.Remove(quickQuestion);
        }
    }
}
