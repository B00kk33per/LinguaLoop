using Avalonia.Controls;
using LinguaLoop.Services;

namespace LinguaLoop.Commands;

public sealed class HintCommand(IWindowCommandContext context, AppLogger logger, Action<string, Exception> onError)
    : LoggedCommand("Hint", logger, onError)
{
    protected override Task ExecuteAsync(object? parameter)
    {
        var card = context.CurrentCard;
        if (card is null) return Task.CompletedTask;
        var literal = context.CurrentExercise?.LiteralTranslation ??
            (string.IsNullOrWhiteSpace(card.Translation) ? "No literal translation is saved for this card." : card.Translation);
        context.HintVocabularyText.Text = card.Prompt;
        context.HintLiteralText.Text = literal;
        var synonyms = context.CurrentExercise?.Synonyms ?? Array.Empty<string>();
        ToolTip.SetTip(context.HintVocabularyText, synonyms.Count > 0
            ? $"Synonyms in the other language: {string.Join(", ", synonyms)}"
            : "No synonyms were returned for this example.");
        ToolTip.SetTip(context.HintLiteralText, $"Literal translation of: {card.Prompt}");
        context.HintPanel.IsVisible = true;
        return Task.CompletedTask;
    }
}
