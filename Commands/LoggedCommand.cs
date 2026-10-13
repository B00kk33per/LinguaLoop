using System.Windows.Input;
using LinguaLoop.Services;

namespace LinguaLoop.Commands;

public abstract class LoggedCommand : ICommand
{
    private readonly string _name;
    private readonly AppLogger _logger;
    private readonly Action<string, Exception> _onError;
    private bool _running;

    protected LoggedCommand(string name, AppLogger logger, Action<string, Exception> onError)
    {
        _name = name;
        _logger = logger;
        _onError = onError;
    }

    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => !_running;
    protected abstract Task ExecuteAsync(object? parameter);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        _running = true;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        _logger.Info($"Command started: {_name}.");
        try
        {
            await ExecuteAsync(parameter);
            _logger.Info($"Command completed: {_name}.");
        }
        catch (Exception exception)
        {
            _logger.Error($"Command failed: {_name}", exception);
            _onError(_name, exception);
        }
        finally
        {
            _running = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
