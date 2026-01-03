using DicomEditor.Services.Interfaces;
using FellowOakDicom;

namespace DicomEditor.Models;

/// <summary>
/// Represents an undoable/redoable action for tag editing.
/// </summary>
public interface IEditAction
{
    string Description { get; }
    void Execute();
    void Undo();
}

/// <summary>
/// Represents a single tag value change.
/// </summary>
public class TagValueChangeAction : IEditAction
{
    private readonly DicomTagItem _tagItem;
    private readonly string _oldValue;
    private readonly string _newValue;
    private readonly Action? _onExecute;
    private readonly Action? _onUndo;

    public string Description => $"Change {_tagItem.TagName} from '{_oldValue}' to '{_newValue}'";

    public TagValueChangeAction(DicomTagItem tagItem, string oldValue, string newValue,
        Action? onExecute = null, Action? onUndo = null)
    {
        _tagItem = tagItem;
        _oldValue = oldValue;
        _newValue = newValue;
        _onExecute = onExecute;
        _onUndo = onUndo;
    }

    public void Execute()
    {
        _tagItem.UpdateValue(_newValue);
        _onExecute?.Invoke();
    }

    public void Undo()
    {
        _tagItem.UpdateValue(_oldValue);
        _onUndo?.Invoke();
    }
}

/// <summary>
/// Represents a bulk tag change across multiple files.
/// </summary>
public class BulkTagChangeAction : IEditAction
{
    private readonly List<(DicomTagItem TagItem, string OldValue, string NewValue)> _changes;
    private readonly Action? _onExecute;
    private readonly Action? _onUndo;

    public string Description { get; }

    public BulkTagChangeAction(string description,
        List<(DicomTagItem TagItem, string OldValue, string NewValue)> changes,
        Action? onExecute = null, Action? onUndo = null)
    {
        Description = description;
        _changes = changes;
        _onExecute = onExecute;
        _onUndo = onUndo;
    }

    public void Execute()
    {
        foreach (var (tagItem, _, newValue) in _changes)
        {
            tagItem.UpdateValue(newValue);
        }
        _onExecute?.Invoke();
    }

    public void Undo()
    {
        foreach (var (tagItem, oldValue, _) in _changes)
        {
            tagItem.UpdateValue(oldValue);
        }
        _onUndo?.Invoke();
    }
}

/// <summary>
/// Represents a bulk tag change across multiple files with dataset updates.
/// This version updates both the UI models and the underlying DICOM datasets.
/// </summary>
public class BulkFileTagChangeAction : IEditAction
{
    private readonly List<BulkFileChange> _changes;
    private readonly IDicomTagService _tagService;

    public string Description { get; }

    public BulkFileTagChangeAction(
        string description,
        List<BulkFileChange> changes,
        IDicomTagService tagService)
    {
        Description = description;
        _changes = changes;
        _tagService = tagService;
    }

    public void Execute()
    {
        foreach (var change in _changes)
        {
            _tagService.UpdateTagValue(change.File.Dataset, change.Tag, change.NewValue);
            change.File.MarkModified();
        }
    }

    public void Undo()
    {
        foreach (var change in _changes)
        {
            _tagService.UpdateTagValue(change.File.Dataset, change.Tag, change.OldValue);
            change.File.MarkModified();
        }
    }
}

/// <summary>
/// Represents a single file change for bulk operations.
/// </summary>
public class BulkFileChange
{
    public DicomFileItem File { get; }
    public DicomTag Tag { get; }
    public string OldValue { get; }
    public string NewValue { get; }

    public BulkFileChange(DicomFileItem file, DicomTag tag, string oldValue, string newValue)
    {
        File = file;
        Tag = tag;
        OldValue = oldValue;
        NewValue = newValue;
    }
}

/// <summary>
/// Represents a tag deletion action.
/// </summary>
public class TagDeleteAction : IEditAction
{
    private readonly DicomTagItem _tagItem;
    private readonly Action<DicomTagItem> _deleteAction;
    private readonly Action<DicomTagItem> _restoreAction;

    public string Description => $"Delete tag {_tagItem.TagName}";

    public TagDeleteAction(DicomTagItem tagItem,
        Action<DicomTagItem> deleteAction,
        Action<DicomTagItem> restoreAction)
    {
        _tagItem = tagItem;
        _deleteAction = deleteAction;
        _restoreAction = restoreAction;
    }

    public void Execute() => _deleteAction(_tagItem);
    public void Undo() => _restoreAction(_tagItem);
}

/// <summary>
/// Represents a bulk tag deletion across multiple files.
/// Stores the deleted tag values for each file to enable undo.
/// </summary>
public class BulkTagDeleteAction : IEditAction
{
    private readonly List<BulkFileTagDeletion> _deletions;
    private readonly IDicomTagService _tagService;

    public string Description { get; }

    public BulkTagDeleteAction(
        string description,
        List<BulkFileTagDeletion> deletions,
        IDicomTagService tagService)
    {
        Description = description;
        _deletions = deletions;
        _tagService = tagService;
    }

    public void Execute()
    {
        foreach (var deletion in _deletions)
        {
            _tagService.RemoveTag(deletion.File.Dataset, deletion.Tag);
            deletion.File.MarkModified();
        }
    }

    public void Undo()
    {
        foreach (var deletion in _deletions)
        {
            // Re-add the tag with the original value
            _tagService.AddTag(deletion.File.Dataset, deletion.Tag, deletion.VR, deletion.OriginalValue);
            deletion.File.MarkModified();
        }
    }
}

/// <summary>
/// Represents a single file's tag deletion for bulk operations.
/// </summary>
public class BulkFileTagDeletion
{
    public DicomFileItem File { get; }
    public DicomTag Tag { get; }
    public string VR { get; }
    public string OriginalValue { get; }

    public BulkFileTagDeletion(DicomFileItem file, DicomTag tag, string vr, string originalValue)
    {
        File = file;
        Tag = tag;
        VR = vr;
        OriginalValue = originalValue;
    }
}
