using LinguaLoop.Services;

namespace LinguaLoop.Commands;

public sealed class ReverseDirectionCommand(IWindowCommandContext context, AppLogger logger, Action<string, Exception> onError)
    : LoggedCommand("Reverse direction", logger, onError)
{
    protected override Task ExecuteAsync(object? parameter)
    {
        if (context.CurrentExercise is null) return Task.CompletedTask;
        context.IsReverseDirection = context.DirectionSwitch.IsChecked == true;
        context.AnswerBox.Text = string.Empty;
        context.FeedbackBox.Text = string.Empty;
        context.UpdateExerciseDisplay();
        context.HintVocabularyText.Text = string.Empty;
        context.HintLiteralText.Text = string.Empty;
        context.HintPanel.IsVisible = false;
        context.AnswerBox.Focus();
        return Task.CompletedTask;
    }
}
