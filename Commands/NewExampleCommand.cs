using LinguaLoop.Services;

namespace LinguaLoop.Commands;

public sealed class NewExampleCommand(IWindowCommandContext context, AppLogger logger, Action<string, Exception> onError)
    : LoggedCommand("New example", logger, onError)
{
    private readonly AppLogger _logger = logger;

    protected override async Task ExecuteAsync(object? parameter)
    {
        var card = context.CurrentCard;
        var coach = context.Coach;
        if (card is null) return;
        if (coach is null)
        {
            context.PracticeStatus.Text = "Set up an AI provider in Settings to generate examples.";
            return;
        }

        var previousExercise = context.CurrentExercise;
        try
        {
            context.SetBusy(true, "Creating another example…");
            context.PracticeStatus.Text = "Creating another example for this vocabulary item…";
            // Read persisted metadata so subsequent examples reuse the established sense.
            var savedCard = await context.Repository.GetCardByIdAsync(card.Id);
            if (savedCard is null)
            {
                context.PracticeStatus.Text = "This vocabulary item was removed. Choose Next for another card.";
                return;
            }

            var recentExamples = (await context.Repository.GetRecentExamplesAsync(card.Id)).ToList();
            if (previousExercise is not null)
            {
                recentExamples.Add(previousExercise.SourceText);
                recentExamples.Add(previousExercise.ReferenceTranslation);
            }

            var exercise = await coach.CreateExerciseAsync(
                savedCard, recentExamples, previousExercise?.SourceLanguage);
            if (!ReferenceEquals(context.CurrentCard, card)) return;

            await context.Repository.SaveLexicalMetadataAsync(
                savedCard, exercise.LiteralTranslation, exercise.Synonyms, exercise.SemanticProfile);
            await context.Repository.SaveGeneratedExampleAsync(card.Id, exercise.SourceText);

            context.CurrentExercise = exercise;
            context.HintPanel.IsVisible = false;
            context.AnswerBox.Text = string.Empty;
            context.FeedbackBox.Text = string.Empty;
            context.CheckButton.IsEnabled = true;
            context.UpdateExerciseDisplay();
            context.AnswerBox.Focus();
        }
        catch (Exception exception)
        {
            _logger.Error($"Could not generate another example for card {card.Id}", exception);
            context.PracticeStatus.Text = "Could not generate another example. Your current exercise and answer are still available; you can retry.";
        }
        finally
        {
            context.SetBusy(false);
        }
    }
}
