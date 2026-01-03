using CommunityToolkit.Mvvm.ComponentModel;

namespace DicomEditor.Core;

/// <summary>
/// Base class for all ViewModels providing common functionality.
/// Uses CommunityToolkit.Mvvm for efficient property change notifications.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _busyMessage;

    public bool IsNotBusy => !IsBusy;

    /// <summary>
    /// Sets the busy state with an optional message.
    /// </summary>
    protected void SetBusy(bool busy, string? message = null)
    {
        IsBusy = busy;
        BusyMessage = message;
    }

    /// <summary>
    /// Executes an action with busy state management.
    /// </summary>
    protected async Task ExecuteWithBusyAsync(Func<Task> action, string? busyMessage = null)
    {
        if (IsBusy) return;

        try
        {
            SetBusy(true, busyMessage);
            await action();
        }
        finally
        {
            SetBusy(false);
        }
    }

    /// <summary>
    /// Executes an action with busy state management and returns a result.
    /// </summary>
    protected async Task<T?> ExecuteWithBusyAsync<T>(Func<Task<T>> action, string? busyMessage = null)
    {
        if (IsBusy) return default;

        try
        {
            SetBusy(true, busyMessage);
            return await action();
        }
        finally
        {
            SetBusy(false);
        }
    }
}
