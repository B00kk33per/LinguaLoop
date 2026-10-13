using System.Diagnostics;

namespace LinguaLoop.Services;

public sealed class AppLogger
{
    private static readonly object Sync = new();
    private readonly string _logPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LinguaLoop",
        "logs",
        "app.log");

    public void Info(string message) => Write("INFO", message);

    public void Error(string operation, Exception exception) =>
        Write("ERROR", $"{operation}: {exception}");

    private void Write(string level, string message)
    {
        var line = $"{DateTimeOffset.UtcNow:O} [{level}] {message}{Environment.NewLine}";
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
                File.AppendAllText(_logPath, line);
            }
        }
        catch (Exception exception)
        {
            Trace.WriteLine($"Could not write LinguaLoop log: {exception.Message}\n{line}");
        }
    }
}
