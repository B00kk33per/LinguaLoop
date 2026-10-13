using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace LinguaLoop;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();
            desktop.MainWindow = mainWindow;

            var trayIcon = new TrayIcon { ToolTipText = "LinguaLoop — vocabulary practice", Icon = CreateTrayIcon() };
            trayIcon.Clicked += (_, _) => mainWindow.ShowForPractice();
            trayIcon.Menu = new NativeMenu
            {
                Items =
                {
                    CreateMenuItem("Practice now", (_, _) => mainWindow.ShowForPractice()),
                    CreateMenuItem("Open vocabulary", (_, _) => mainWindow.ShowForVocabulary()),
                    new NativeMenuItemSeparator(),
                    CreateMenuItem("Exit", (_, _) => desktop.Shutdown())
                }
            };
            TrayIcon.SetIcons(this, new TrayIcons { trayIcon });
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static NativeMenuItem CreateMenuItem(string header, EventHandler handler)
    {
        var item = new NativeMenuItem(header);
        item.Click += handler;
        return item;
    }

    private static WindowIcon CreateTrayIcon()
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(32, 32), new Vector(96, 96));
        using var context = bitmap.CreateDrawingContext();
        context.DrawRectangle(new SolidColorBrush(Color.Parse("#285DA8")), null, new Rect(0, 0, 32, 32));
        context.DrawGeometry(Brushes.White, null, new EllipseGeometry(new Rect(8, 8, 16, 16)));
        return new WindowIcon(bitmap);
    }
}
