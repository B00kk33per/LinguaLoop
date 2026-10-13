using LinguaLoop.Domain;
using LinguaLoop.Services;

namespace LinguaLoop.Commands;

public sealed class SaveSettingsCommand(IWindowCommandContext context, AppLogger logger, Action<string, Exception> onError)
    : LoggedCommand("Save settings", logger, onError)
{
    protected override async Task ExecuteAsync(object? parameter)
    {
        var settings = context.Settings;
        settings.Provider = context.ProviderBox.SelectedIndex == 1 ? AppSettings.GeminiProvider : AppSettings.CloudflareProvider;
        settings.CloudflareAccountId = context.CloudflareAccountIdBox.Text?.Trim();
        var token = context.ProviderTokenBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(token))
        {
            if (string.Equals(settings.Provider, AppSettings.CloudflareProvider, StringComparison.OrdinalIgnoreCase))
                settings.ProtectedCloudflareApiToken = SettingsStore.Protect(token);
            else settings.ProtectedGeminiApiKey = SettingsStore.Protect(token);
        }
        settings.Model = string.IsNullOrWhiteSpace(context.ModelBox.Text) ? context.DefaultModelForSelectedProvider() : context.ModelBox.Text.Trim();
        settings.QuizIntervalMinutes = Convert.ToInt32(context.IntervalBox.Value ?? 30);
        await context.SettingsStore.SaveAsync(settings);
        context.ProviderTokenBox.Text = string.Empty;
        context.Coach = PracticeCoachFactory.Create(settings);
        context.SettingsStatus.Text = context.GetSettingsStatus();
        context.RestartQuizTimer();
        await context.LoadNextCardAsync();
    }
}
