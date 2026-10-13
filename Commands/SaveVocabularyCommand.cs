using Avalonia.Controls;
using LinguaLoop.Services;

namespace LinguaLoop.Commands;

public sealed class SaveVocabularyCommand(IWindowCommandContext context, AppLogger logger, Action<string, Exception> onError)
    : LoggedCommand("Save vocabulary", logger, onError)
{
    protected override async Task ExecuteAsync(object? parameter)
    {
        if (context.SelectedVocabularyCard is not { } card) return;
        var prompt = context.NewPrompt.Text?.Trim();
        if (string.IsNullOrWhiteSpace(prompt)) { context.AddStatus.Text = "A word or sentence is required."; return; }
        var language = (context.PromptLanguage.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "English";
        await context.Repository.UpdateAsync(card.Id, prompt, context.NewTranslation.Text, language);
        context.ResetVocabularyEditor();
        context.AddStatus.Text = "Changes saved. Review progress was kept.";
        await context.RefreshVocabularyAsync();
        await context.RefreshStatisticsAsync();
    }
}
