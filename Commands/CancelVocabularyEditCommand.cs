using LinguaLoop.Services;

namespace LinguaLoop.Commands;

public sealed class CancelVocabularyEditCommand(IWindowCommandContext context, AppLogger logger, Action<string, Exception> onError)
    : LoggedCommand("Cancel vocabulary edit", logger, onError)
{
    protected override Task ExecuteAsync(object? parameter)
    {
        context.ResetVocabularyEditor();
        context.AddStatus.Text = "Edit cancelled.";
        return Task.CompletedTask;
    }
}
