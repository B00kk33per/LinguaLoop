using Avalonia.Controls;
using Avalonia.Input.Platform;
using LinguaLoop.Services;

namespace LinguaLoop.Commands;

public sealed class CopySentenceCommand(IWindowCommandContext context, AppLogger logger, Action<string, Exception> onError)
    : LoggedCommand("Copy sentence", logger, onError)
{
    protected override async Task ExecuteAsync(object? parameter)
    {
        var text = context.DisplayPromptTextValue;
        if (string.IsNullOrWhiteSpace(text)) return;
        var clipboard = TopLevel.GetTopLevel(context.Owner)?.Clipboard;
        if (clipboard is null) { context.PracticeStatus.Text = "Clipboard is unavailable."; return; }
        await clipboard.SetTextAsync(text);
        context.PracticeStatus.Text = "Example copied. Paste it into Vocabulary.";
    }
}
