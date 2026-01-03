using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DicomEditor.Core;
using DicomEditor.Models;
using DicomEditor.Services.Interfaces;

namespace DicomEditor.ViewModels;

/// <summary>
/// ViewModel for the tag editor panel.
/// Handles individual tag editing with validation.
/// </summary>
public partial class TagEditorViewModel : ViewModelBase
{
    private readonly IDicomTagService _tagService;
    private readonly IDicomValidationService _validationService;

    [ObservableProperty]
    private DicomTagItem? _tag;

    [ObservableProperty]
    private string? _editValue;

    [ObservableProperty]
    private bool _hasValidationError;

    [ObservableProperty]
    private string? _validationMessage;

    [ObservableProperty]
    private bool _isEditMode;

    public event EventHandler<TagValueChangedEventArgs>? ValueChanged;

    public TagEditorViewModel(
        IDicomTagService tagService,
        IDicomValidationService validationService)
    {
        _tagService = tagService;
        _validationService = validationService;
    }

    public void SetTag(DicomTagItem? tag)
    {
        Tag = tag;
        EditValue = tag?.ValueDisplay;
        HasValidationError = false;
        ValidationMessage = null;
        IsEditMode = false;
    }

    partial void OnEditValueChanged(string? value)
    {
        if (Tag == null || value == null) return;

        // Real-time validation
        var result = _validationService.ValidateValue(Tag.Tag, Tag.VR, value);
        HasValidationError = !result.IsValid;
        ValidationMessage = result.ErrorMessage;
    }

    [RelayCommand]
    private void BeginEdit()
    {
        if (Tag?.IsEditable == true)
        {
            IsEditMode = true;
            EditValue = Tag.ValueDisplay;
        }
    }

    [RelayCommand]
    private void CommitEdit()
    {
        if (Tag == null || HasValidationError) return;

        var oldValue = Tag.ValueDisplay;
        var newValue = EditValue ?? string.Empty;

        if (oldValue != newValue)
        {
            ValueChanged?.Invoke(this, new TagValueChangedEventArgs(Tag, oldValue, newValue));
        }

        IsEditMode = false;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        EditValue = Tag?.ValueDisplay;
        HasValidationError = false;
        ValidationMessage = null;
        IsEditMode = false;
    }
}

public class TagValueChangedEventArgs : EventArgs
{
    public DicomTagItem Tag { get; }
    public string OldValue { get; }
    public string NewValue { get; }

    public TagValueChangedEventArgs(DicomTagItem tag, string oldValue, string newValue)
    {
        Tag = tag;
        OldValue = oldValue;
        NewValue = newValue;
    }
}
