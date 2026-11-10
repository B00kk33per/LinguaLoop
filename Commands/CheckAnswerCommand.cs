using LinguaLoop.Domain;
using LinguaLoop.Services;

namespace LinguaLoop.Commands;

public sealed class CheckAnswerCommand(IWindowCommandContext context, AppLogger logger, Action<string, Exception> onError)
    : LoggedCommand("Check answer", logger, onError)
{
    protected override async Task ExecuteAsync(object? parameter)
    {
        var card = context.CurrentCard;
        if (card is null) { context.PracticeStatus.Text = "There is no active card to check."; return; }
        var answer = context.AnswerBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(answer)) { context.FeedbackBox.Text = "Write an answer first, or choose Next."; return; }
        try
        {
            context.PracticeStatus.Text = "Checking your answer…";
            context.SetBusy(true, "Checking your answer…");
            var exercise = context.CurrentExerciseForDirection();
            var isDirectCardFallback = context.Coach is not null && context.CurrentExercise is null;
            var grade = context.CurrentExercise is not null && context.Coach is not null
                ? await context.Coach.GradeAsync(exercise!, answer)
                : LocalCoach.Grade(card, answer);
            var direction = context.IsReverseDirection ? ReviewDirection.Reverse : ReviewDirection.Standard;
            var isRetry = context.IsCurrentCardReviewed;
            if (!isRetry)
            {
                await context.Repository.RecordReviewAsync(card, grade.IsCorrect, grade.Feedback, direction);
                context.IsCurrentCardReviewed = true;
                await context.RefreshStatisticsAsync();
            }
            var directionNote = context.IsReverseDirection ? "Reverse direction — " : string.Empty;
            var fallbackNote = isDirectCardFallback
                ? "The natural example was unavailable, so this direct-card answer was checked against the saved meanings.\n"
                : string.Empty;
            context.FeedbackBox.Text = $"{fallbackNote}{(grade.IsCorrect ? "✓ Correct" : "Try again soon")} — {directionNote}{grade.Feedback}\nNatural answer: {grade.IdealAnswer}";
            context.PracticeStatus.Text = isRetry
                ? "Practice retry checked. Your learning score still reflects the first attempt."
                : "Answer recorded. You can revise and check again, or choose Next for another card.";
        }
        catch (GradingContextMismatchException exception)
        {
            context.PracticeStatus.Text = "The coach response did not match this exercise. Your answer was not recorded.";
            context.FeedbackBox.Text = $"{exception.Message}\nYou can safely press Check answer again; your previous attempts were not recorded.";
        }
        catch (Exception exception)
        {
            context.PracticeStatus.Text = "Could not reach the AI coach.";
            context.FeedbackBox.Text = $"{exception.Message}\nYour answer was not recorded; you can retry safely.";
            throw;
        }
        finally { context.SetBusy(false); }
    }
}
