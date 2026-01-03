using DicomEditor.Models;

namespace DicomEditor.Services.Interfaces;

/// <summary>
/// Service interface for managing application settings.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets the current settings.
    /// </summary>
    AppSettings Settings { get; }

    /// <summary>
    /// Loads settings from persistent storage.
    /// </summary>
    Task LoadAsync();

    /// <summary>
    /// Saves settings to persistent storage.
    /// </summary>
    Task SaveAsync();

    /// <summary>
    /// Adds a folder to the recent folders list.
    /// </summary>
    void AddRecentFolder(string folderPath);

    /// <summary>
    /// Event raised when settings change.
    /// </summary>
    event EventHandler? SettingsChanged;
}

/// <summary>
/// Service interface for file system dialogs.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Shows an open file dialog.
    /// </summary>
    string[]? ShowOpenFileDialog(string title, string filter, bool multiSelect = false);

    /// <summary>
    /// Shows a folder browser dialog.
    /// </summary>
    string? ShowFolderBrowserDialog(string title, string? initialDirectory = null);

    /// <summary>
    /// Shows a save file dialog.
    /// </summary>
    string? ShowSaveFileDialog(string title, string filter, string? defaultFileName = null);

    /// <summary>
    /// Shows a message box.
    /// </summary>
    bool ShowConfirmation(string message, string title);

    /// <summary>
    /// Shows an error message.
    /// </summary>
    void ShowError(string message, string title);

    /// <summary>
    /// Shows an information message.
    /// </summary>
    void ShowInfo(string message, string title);

    /// <summary>
    /// Shows an input dialog for entering a value.
    /// </summary>
    /// <param name="title">Dialog title</param>
    /// <param name="message">Prompt message</param>
    /// <param name="defaultValue">Default value in the text box</param>
    /// <returns>The entered value, or null if cancelled</returns>
    string? ShowInputDialog(string title, string message, string? defaultValue = null);
}
