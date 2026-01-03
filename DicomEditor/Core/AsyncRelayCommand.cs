using System.Windows.Input;

namespace DicomEditor.Core;

/// <summary>
/// Async command implementation that prevents multiple executions
/// and provides cancellation support.
/// </summary>
public class AsyncRelayCommand : ICommand
{
    private readonly Func<CancellationToken, Task> _execute;
    private readonly Func<bool>? _canExecute;
    private CancellationTokenSource? _cts;
    private bool _isExecuting;

    public event EventHandler? CanExecuteChanged;

    public AsyncRelayCommand(Func<CancellationToken, Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute)
    {
    }

    public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;

        _isExecuting = true;
        _cts = new CancellationTokenSource();
        RaiseCanExecuteChanged();

        try
        {
            await _execute(_cts.Token);
        }
        finally
        {
            _isExecuting = false;
            _cts?.Dispose();
            _cts = null;
            RaiseCanExecuteChanged();
        }
    }

    public void Cancel() => _cts?.Cancel();

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// Generic async command with parameter support.
/// </summary>
public class AsyncRelayCommand<T> : ICommand
{
    private readonly Func<T?, CancellationToken, Task> _execute;
    private readonly Func<T?, bool>? _canExecute;
    private CancellationTokenSource? _cts;
    private bool _isExecuting;

    public event EventHandler? CanExecuteChanged;

    public AsyncRelayCommand(Func<T?, CancellationToken, Task> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke((T?)parameter) ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;

        _isExecuting = true;
        _cts = new CancellationTokenSource();
        RaiseCanExecuteChanged();

        try
        {
            await _execute((T?)parameter, _cts.Token);
        }
        finally
        {
            _isExecuting = false;
            _cts?.Dispose();
            _cts = null;
            RaiseCanExecuteChanged();
        }
    }

    public void Cancel() => _cts?.Cancel();

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
