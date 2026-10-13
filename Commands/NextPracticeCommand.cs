using LinguaLoop.Domain;
using LinguaLoop.Services;

namespace LinguaLoop.Commands;

public sealed class NextPracticeCommand(IWindowCommandContext context, AppLogger logger, Action<string, Exception> onError)
    : LoggedCommand("Next", logger, onError)
{
    protected override async Task ExecuteAsync(object? parameter)
    {
        if (context.CurrentCard is { } card)
            await context.Repository.RecordReviewAsync(card, false, "Skipped by learner.", context.IsReverseDirection ? ReviewDirection.Reverse : ReviewDirection.Standard);
        await context.LoadNextCardAsync();
    }
}
