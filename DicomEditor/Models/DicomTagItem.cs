using CommunityToolkit.Mvvm.ComponentModel;
using FellowOakDicom;

namespace DicomEditor.Models;

/// <summary>
/// Represents a single DICOM tag with its value for display and editing.
/// Supports hierarchical structure for sequence items.
/// </summary>
public partial class DicomTagItem : ObservableObject
{
    [ObservableProperty]
    private DicomTag _tag;

    [ObservableProperty]
    private string _tagDisplay;

    [ObservableProperty]
    private string _tagName;

    [ObservableProperty]
    [property: System.Text.Json.Serialization.JsonPropertyName("VR")]
    private string _vR; // Value Representation - named _vR to generate VR property

    [ObservableProperty]
    private string _valueDisplay;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isEditable = true;

    [ObservableProperty]
    private bool _isSequence;

    [ObservableProperty]
    private bool _isPrivateTag;

    [ObservableProperty]
    private int _nestingLevel;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _hasValidationError;

    [ObservableProperty]
    private string? _validationErrorMessage;

    [ObservableProperty]
    private bool _hasEmptyValue;

    /// <summary>
    /// Gets the private creator identifier for private tags.
    /// </summary>
    [ObservableProperty]
    private string? _privateCreator;

    /// <summary>
    /// Child items for sequence tags.
    /// </summary>
    public List<DicomTagItem> Children { get; } = new();

    /// <summary>
    /// Reference to the parent item (for nested sequences).
    /// </summary>
    public DicomTagItem? Parent { get; set; }

    /// <summary>
    /// Reference to the source DicomItem for editing.
    /// </summary>
    public DicomItem? SourceItem { get; set; }

    public DicomTagItem(DicomTag tag, string vr, string value, int nestingLevel = 0)
    {
        _tag = tag;
        _tagDisplay = $"({tag.Group:X4},{tag.Element:X4})";
        _tagName = GetTagName(tag);
        _vR = vr;
        _valueDisplay = value;
        _nestingLevel = nestingLevel;
        _isSequence = vr == "SQ";
        _isPrivateTag = tag.IsPrivate;
        _hasEmptyValue = string.IsNullOrEmpty(value);
        _privateCreator = tag.IsPrivate ? tag.PrivateCreator?.Creator : null;
        
        // Some VRs should not be directly editable
        _isEditable = !IsReadOnlyVR(vr);
    }

    /// <summary>
    /// Gets the tag name, handling private tags specially.
    /// </summary>
    private static string GetTagName(DicomTag tag)
    {
        if (tag.IsPrivate)
        {
            // For private tags, try to get name from dictionary entry
            var name = tag.DictionaryEntry?.Name;
            if (!string.IsNullOrEmpty(name) && name != "Unknown")
            {
                return name;
            }

            // Include private creator if available
            if (tag.PrivateCreator != null)
            {
                return $"Private: {tag.PrivateCreator.Creator}";
            }

            return "Private Tag";
        }

        return tag.DictionaryEntry?.Name ?? "Unknown Tag";
    }

    /// <summary>
    /// Determines if a VR type should be read-only in the editor.
    /// </summary>
    private static bool IsReadOnlyVR(string vr)
    {
        return vr switch
        {
            "OB" or "OW" or "OF" or "OD" or "OL" or "OV" or "UN" => true, // Binary data
            "SQ" => true, // Sequences need special handling
            _ => false
        };
    }

    /// <summary>
    /// Updates the displayed value.
    /// </summary>
    public void UpdateValue(string newValue)
    {
        if (ValueDisplay != newValue)
        {
            ValueDisplay = newValue;
            HasEmptyValue = string.IsNullOrEmpty(newValue);
        }
    }
}
