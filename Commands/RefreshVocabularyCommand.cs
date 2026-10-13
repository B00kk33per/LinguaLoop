using LinguaLoop.Services;

namespace LinguaLoop.Commands;

public sealed class RefreshVocabularyCommand(IWindowCommandContext context, AppLogger logger, Action<string, Exception> onError)
    : LoggedCommand("Refresh vocabulary", logger, onError)
{
    protected override Task ExecuteAsync(object? parameter) => context.RefreshVocabularyAsync();
}
