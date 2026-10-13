using Avalonia.Labs.Notifications;

namespace LinguaLoop.Services;

public enum PracticeReminderAction
{
    Answer,
    Skip
}

/// <summary>Displays the scheduled practice prompt as an actionable native notification.</summary>
public sealed class PracticeReminderService : IDisposable
{
    private const string AnswerAction = "answer";
    private const string SkipAction = "skip";

    private readonly AppLogger _logger;
    private readonly INativeNotificationManager? _manager;
    private INativeNotification? _notification;
    private uint? _activeNotificationId;

    public PracticeReminderService(AppLogger logger)
    {
        _logger = logger;
        _manager = NativeNotificationManager.Current;
        if (_manager is null)
        {
            _logger.Info("Native notifications are unavailable on this platform.");
            return;
        }

        _manager.NotificationCompleted += OnNotificationCompleted;
    }

    public event EventHandler<PracticeReminderAction>? ActionSelected;

    public bool Show()
    {
        if (_manager is null)
            return false;

        _notification?.Close();
        _notification = _manager.CreateNotification(null);
        if (_notification is null)
        {
            _logger.Info("The native notification manager did not create a reminder.");
            return false;
        }

        _notification.Title = "LinguaLoop";
        _notification.Message = "Time for a short vocabulary practice.";
        _notification.SetActions(
        [
            new NativeNotificationAction("Answer", AnswerAction),
            new NativeNotificationAction("Skip", SkipAction)
        ]);
        _activeNotificationId = _notification.Id;
        _notification.Show();
        _logger.Info($"Displayed practice reminder notification {_notification.Id}.");
        return true;
    }

    private void OnNotificationCompleted(object? sender, NativeNotificationCompletedEventArgs e)
    {
        if (e.NotificationId != _activeNotificationId)
            return;

        _notification = null;
        _activeNotificationId = null;
        var action = e.ActionTag switch
        {
            AnswerAction => PracticeReminderAction.Answer,
            SkipAction => PracticeReminderAction.Skip,
            _ => (PracticeReminderAction?)null
        };

        if (action is null)
            return;

        _logger.Info($"Practice reminder action selected: {action.Value}.");
        ActionSelected?.Invoke(this, action.Value);
    }

    public void Dispose()
    {
        if (_manager is not null)
            _manager.NotificationCompleted -= OnNotificationCompleted;
        _notification?.Close();
    }
}
