using DicomEditor.Models;

namespace DicomEditor.Services.Interfaces;

/// <summary>
/// Service interface for managing undo/redo operations.
/// </summary>
public interface IUndoRedoService
{
    /// <summary>
    /// Indicates if there are actions to undo.
    /// </summary>
    bool CanUndo { get; }

    /// <summary>
    /// Indicates if there are actions to redo.
    /// </summary>
    bool CanRedo { get; }

    /// <summary>
    /// Gets the description of the next undo action.
    /// </summary>
    string? UndoDescription { get; }

    /// <summary>
    /// Gets the description of the next redo action.
    /// </summary>
    string? RedoDescription { get; }

    /// <summary>
    /// Event raised when undo/redo state changes.
    /// </summary>
    event EventHandler? StateChanged;

    /// <summary>
    /// Executes an action and adds it to the undo stack.
    /// </summary>
    void Execute(IEditAction action);

    /// <summary>
    /// Undoes the last action.
    /// </summary>
    void Undo();

    /// <summary>
    /// Redoes the last undone action.
    /// </summary>
    void Redo();

    /// <summary>
    /// Clears all undo/redo history.
    /// </summary>
    void Clear();

    /// <summary>
    /// Begins a batch operation (multiple actions as one undo).
    /// </summary>
    void BeginBatch(string description);

    /// <summary>
    /// Ends a batch operation.
    /// </summary>
    void EndBatch();
}
