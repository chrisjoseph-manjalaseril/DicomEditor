using System.Windows;
using DicomEditor.Controls;
using DicomEditor.Services.Interfaces;
using Microsoft.Win32;

namespace DicomEditor.Services;

/// <summary>
/// Implementation of IDialogService.
/// Provides file dialogs and message boxes for the WPF application.
/// </summary>
public class DialogService : IDialogService
{
    public string[]? ShowOpenFileDialog(string title, string filter, bool multiSelect = false)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            Multiselect = multiSelect
        };

        return dialog.ShowDialog() == true ? dialog.FileNames : null;
    }

    public string? ShowFolderBrowserDialog(string title, string? initialDirectory = null)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            InitialDirectory = initialDirectory
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public string? ShowSaveFileDialog(string title, string filter, string? defaultFileName = null)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = filter,
            FileName = defaultFileName ?? string.Empty
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public bool ShowConfirmation(string message, string title)
    {
        var result = MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        return result == MessageBoxResult.Yes;
    }

    public void ShowError(string message, string title)
    {
        MessageBox.Show(
            message,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    public void ShowInfo(string message, string title)
    {
        MessageBox.Show(
            message,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    public string? ShowInputDialog(string title, string message, string? defaultValue = null)
    {
        return InputDialog.Show(title, message, defaultValue);
    }
}
