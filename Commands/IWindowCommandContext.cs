using Avalonia.Controls;
using LinguaLoop.Domain;
using LinguaLoop.Services;

namespace LinguaLoop.Commands;

/// <summary>UI and application services exposed to commands, keeping action logic out of the window.</summary>
public interface IWindowCommandContext
{
    Window Owner { get; }
    VocabularyRepository Repository { get; }
    SettingsStore SettingsStore { get; }
    AppSettings Settings { get; }
    Card? CurrentCard { get; set; }
    PracticeExercise? CurrentExercise { get; set; }
    IPracticeCoach? Coach { get; set; }
    string? AiLoadError { get; }
    bool IsReverseDirection { get; set; }
    string DisplayPromptTextValue { get; }
    Card? SelectedVocabularyCard { get; }
    TextBox AnswerBox { get; }
    TextBox NewPrompt { get; }
    TextBox NewTranslation { get; }
    TextBox ProviderTokenBox { get; }
    TextBox CloudflareAccountIdBox { get; }
    TextBox ModelBox { get; }
    TextBlock PracticeStatus { get; }
    TextBlock FeedbackBox { get; }
    TextBlock HintVocabularyText { get; }
    TextBlock HintLiteralText { get; }
    TextBlock AddStatus { get; }
    TextBlock SettingsStatus { get; }
    ToggleSwitch DirectionSwitch { get; }
    ComboBox PromptLanguage { get; }
    ComboBox ProviderBox { get; }
    NumericUpDown IntervalBox { get; }
    ListBox VocabularyList { get; }
    Border HintPanel { get; }
    Task LoadNextCardAsync();
    Task RefreshVocabularyAsync();
    Task RefreshStatisticsAsync();
    void ResetVocabularyEditor();
    void DisplayPromptText(string text);
    void UpdateExerciseDisplay();
    PracticeExercise? CurrentExerciseForDirection();
    void SetBusy(bool isBusy, string? message = null);
    void RestartQuizTimer();
    string DefaultModelForSelectedProvider();
    string GetSettingsStatus();
}
