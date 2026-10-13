using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using LinguaLoop.Services;

namespace LinguaLoop.Commands;

public sealed class RemoveVocabularyCommand(IWindowCommandContext context, AppLogger logger, Action<string, Exception> onError)
    : LoggedCommand("Remove vocabulary", logger, onError)
{
    protected override async Task ExecuteAsync(object? parameter)
    {
        if (context.SelectedVocabularyCard is not { } card) return;
        var cancel = new Button { Content = "Cancel", MinWidth = 90 };
        var remove = new Button { Content = "Remove", MinWidth = 90 };
        var dialog = new Window
        {
            Title = "Remove vocabulary item", Width = 420, Height = 190, CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(20), Spacing = 18,
                Children =
                {
                    new TextBlock { Text = $"Remove ‘{card.Prompt}’ from your vocabulary? Its review history and generated examples will also be deleted.", TextWrapping = TextWrapping.Wrap },
                    new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Children = { cancel, remove } }
                }
            }
        };
        cancel.Click += (_, _) => dialog.Close(false);
        remove.Click += (_, _) => dialog.Close(true);
        if (!await dialog.ShowDialog<bool>(context.Owner)) return;
        var removedActiveCard = context.CurrentCard?.Id == card.Id;
        await context.Repository.RemoveAsync(card.Id);
        context.ResetVocabularyEditor();
        context.AddStatus.Text = $"Removed ‘{card.Prompt}’ and its review history.";
        await context.RefreshVocabularyAsync();
        await context.RefreshStatisticsAsync();
        if (removedActiveCard)
        {
            context.CurrentCard = null;
            context.CurrentExercise = null;
            context.AnswerBox.Text = context.FeedbackBox.Text = string.Empty;
            context.DisplayPromptText("This card was removed from your vocabulary.");
            context.PracticeStatus.Text = "The active card was removed.";
        }
    }
}
