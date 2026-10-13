using LinguaLoop.Services;

namespace LinguaLoop.Commands;

public sealed class RefreshStatisticsCommand(IWindowCommandContext context, AppLogger logger, Action<string, Exception> onError)
    : LoggedCommand("Refresh statistics", logger, onError)
{
    protected override Task ExecuteAsync(object? parameter) => context.RefreshStatisticsAsync();
}
