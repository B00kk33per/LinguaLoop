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
        if (context.Coach is not null && context.CurrentExercise is null && !string.IsNullOrWhiteSpace(context.AiLoadError))
        {
            context.PracticeStatus.Text = "The AI coach could not prepare this exercise. Your answer was not recorded.";
            context.FeedbackBox.Text = context.AiLoadError;
            return;
        }

        try
        {
            context.PracticeStatus.Text = "Checking your answer…";
            context.SetBusy(true, "Checking your answer…");
            var exercise = context.CurrentExerciseForDirection();
            var grade = context.CurrentExercise is not null && context.Coach is not null
                ? await context.Coach.GradeAsync(exercise!, answer)
                : LocalCoach.Grade(card, answer);
            var direction = context.IsReverseDirection ? ReviewDirection.Reverse : ReviewDirection.Standard;
            await context.Repository.RecordReviewAsync(card, grade.IsCorrect, grade.Feedback, direction);
            var directionNote = context.IsReverseDirection ? "Reverse direction — " : string.Empty;
            context.FeedbackBox.Text = $"{(grade.IsCorrect ? "✓ Correct" : "Try again soon")} — {directionNote}{grade.Feedback}\nNatural answer: {grade.IdealAnswer}";
            context.PracticeStatus.Text = context.IsReverseDirection
                ? "Recorded as a reverse-direction answer with a lighter effect on your learning score."
                : grade.IsCorrect ? "Great work. This card will appear less often." : "This card returns in about ten minutes.";
            await context.RefreshStatisticsAsync();
            context.CurrentCard = null;
            context.CurrentExercise = null;
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
