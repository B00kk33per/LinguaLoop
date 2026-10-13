using Avalonia;
using Avalonia.Labs.Notifications;
using System;
using LinguaLoop.Services;

namespace LinguaLoop;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "--add", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: --add <prompt> [translation-or--] [English|Ukrainian]");
                return;
            }

            var repository = new VocabularyRepository();
            repository.InitializeAsync().GetAwaiter().GetResult();
            var translation = args.Length > 2 && args[2] != "--" ? args[2] : null;
            var language = args.Length > 3 ? args[3] : "English";
            repository.AddAsync(args[1], translation, language).GetAwaiter().GetResult();
            Console.WriteLine($"Added: {args[1]}");
            return;
        }

        if (args.Length == 2 && string.Equals(args[0], "--remove-exact", StringComparison.OrdinalIgnoreCase))
        {
            var repository = new VocabularyRepository();
            repository.InitializeAsync().GetAwaiter().GetResult();
            repository.RemoveByExactPromptAsync(args[1]).GetAwaiter().GetResult();
            Console.WriteLine($"Removed: {args[1]}");
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithAppNotifications(new AppNotificationOptions
            {
                AppName = "LinguaLoop",
                AppUserModelId = "LinguaLoop.Desktop"
            })
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
