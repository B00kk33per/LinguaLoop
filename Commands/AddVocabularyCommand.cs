using Avalonia.Controls;
using LinguaLoop.Services;

namespace LinguaLoop.Commands;

public sealed class AddVocabularyCommand(IWindowCommandContext context, AppLogger logger, Action<string, Exception> onError)
    : LoggedCommand("Add vocabulary", logger, onError)
{
    protected override async Task ExecuteAsync(object? parameter)
    {
        var prompt = context.NewPrompt.Text?.Trim();
        if (string.IsNullOrWhiteSpace(prompt)) { context.AddStatus.Text = "A word or sentence is required."; return; }
        var language = (context.PromptLanguage.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "English";
        await context.Repository.AddAsync(prompt, context.NewTranslation.Text, language);
        context.NewPrompt.Text = context.NewTranslation.Text = string.Empty;
        context.AddStatus.Text = "Added. It is ready to practise now.";
        await context.RefreshVocabularyAsync();
        await context.RefreshStatisticsAsync();
        await context.LoadNextCardAsync();
    }
}
