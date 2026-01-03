using DicomEditor.Models;
using DicomEditor.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace DicomEditor.Services;

/// <summary>
/// Implementation of IUndoRedoService.
/// Manages undo/redo stacks for edit operations.
/// </summary>
public class UndoRedoService : IUndoRedoService
{
    private readonly Stack<IEditAction> _undoStack = new();
    private readonly Stack<IEditAction> _redoStack = new();
    private readonly ILogger<UndoRedoService> _logger;
    private BatchAction? _currentBatch;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
    public string? UndoDescription => _undoStack.TryPeek(out var action) ? action.Description : null;
    public string? RedoDescription => _redoStack.TryPeek(out var action) ? action.Description : null;

    public event EventHandler? StateChanged;

    public UndoRedoService(ILogger<UndoRedoService> logger)
    {
        _logger = logger;
    }

    public void Execute(IEditAction action)
    {
        if (_currentBatch != null)
        {
            // Add to current batch instead of executing
            _currentBatch.Actions.Add(action);
            action.Execute();
        }
        else
        {
            action.Execute();
            _undoStack.Push(action);
            _redoStack.Clear(); // Clear redo stack on new action
            OnStateChanged();
        }

        _logger.LogDebug("Executed action: {Description}", action.Description);
    }

    public void Undo()
    {
        if (!CanUndo) return;

        var action = _undoStack.Pop();
        action.Undo();
        _redoStack.Push(action);
        OnStateChanged();

        _logger.LogDebug("Undid action: {Description}", action.Description);
    }

    public void Redo()
    {
        if (!CanRedo) return;

        var action = _redoStack.Pop();
        action.Execute();
        _undoStack.Push(action);
        OnStateChanged();

        _logger.LogDebug("Redid action: {Description}", action.Description);
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        _currentBatch = null;
        OnStateChanged();

        _logger.LogDebug("Cleared undo/redo history");
    }

    public void BeginBatch(string description)
    {
        _currentBatch = new BatchAction(description);
        _logger.LogDebug("Started batch: {Description}", description);
    }

    public void EndBatch()
    {
        if (_currentBatch == null) return;

        if (_currentBatch.Actions.Count > 0)
        {
            _undoStack.Push(_currentBatch);
            _redoStack.Clear();
            OnStateChanged();
        }

        _logger.LogDebug("Ended batch: {Description} with {Count} actions",
            _currentBatch.Description, _currentBatch.Actions.Count);
        _currentBatch = null;
    }

    private void OnStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Represents a batch of actions that can be undone/redone as a single operation.
    /// </summary>
    private class BatchAction : IEditAction
    {
        public string Description { get; }
        public List<IEditAction> Actions { get; } = new();

        public BatchAction(string description)
        {
            Description = description;
        }

        public void Execute()
        {
            foreach (var action in Actions)
            {
                action.Execute();
            }
        }

        public void Undo()
        {
            // Undo in reverse order
            for (int i = Actions.Count - 1; i >= 0; i--)
            {
                Actions[i].Undo();
            }
        }
    }
}
