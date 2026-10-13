using LinguaLoop.Services;

namespace LinguaLoop.Commands;

public sealed class DoneCommand(IWindowCommandContext context, AppLogger logger, Action<string, Exception> onError)
    : LoggedCommand("Done", logger, onError)
{
    protected override Task ExecuteAsync(object? parameter) { context.Owner.Close(); return Task.CompletedTask; }
}
