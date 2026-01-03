using DicomEditor.Core;
using DicomEditor.Models;
using DicomEditor.Services.Interfaces;
using FellowOakDicom;
using Microsoft.Extensions.Logging;

namespace DicomEditor.Services;

/// <summary>
/// Implementation of IDicomTagService.
/// Handles DICOM tag extraction, modification, and searching.
/// </summary>
public class DicomTagService : IDicomTagService
{
    private readonly ILogger<DicomTagService> _logger;
    private readonly IDicomValidationService _validationService;

    public DicomTagService(ILogger<DicomTagService> logger, IDicomValidationService validationService)
    {
        _logger = logger;
        _validationService = validationService;
    }

    public IEnumerable<DicomTagItem> ExtractTags(DicomDataset dataset, bool includePrivateTags = true)
    {
        // Walk the dataset to extract all tags
        foreach (var item in dataset)
        {
            // Filter private tags if requested
            if (!includePrivateTags && item.Tag.IsPrivate)
                continue;

            yield return CreateTagItem(item, dataset, 0);

            // Also extract sequence items recursively for flat view
            if (item is DicomSequence sequence)
            {
                foreach (var seqItem in ExtractSequenceItemsFlat(sequence, dataset, includePrivateTags, 1))
                {
                    yield return seqItem;
                }
            }
        }
    }

    /// <summary>
    /// Extracts sequence items in a flat structure for display.
    /// </summary>
    private IEnumerable<DicomTagItem> ExtractSequenceItemsFlat(DicomSequence sequence, DicomDataset parentDataset, bool includePrivateTags, int level)
    {
        int itemIndex = 0;
        foreach (var sequenceItem in sequence.Items)
        {
            foreach (var item in sequenceItem)
            {
                if (!includePrivateTags && item.Tag.IsPrivate)
                    continue;

                var tagItem = CreateTagItem(item, sequenceItem, level);
                yield return tagItem;

                // Recurse into nested sequences
                if (item is DicomSequence nestedSequence)
                {
                    foreach (var nestedItem in ExtractSequenceItemsFlat(nestedSequence, sequenceItem, includePrivateTags, level + 1))
                    {
                        yield return nestedItem;
                    }
                }
            }
            itemIndex++;
        }
    }

    public IEnumerable<DicomTagItem> ExtractTagsHierarchical(DicomDataset dataset, bool includePrivateTags = true)
    {
        return ExtractTagsRecursive(dataset, includePrivateTags, 0);
    }

    private IEnumerable<DicomTagItem> ExtractTagsRecursive(DicomDataset dataset, bool includePrivateTags, int level)
    {
        foreach (var item in dataset)
        {
            if (!includePrivateTags && item.Tag.IsPrivate)
                continue;

            var tagItem = CreateTagItem(item, dataset, level);

            if (item is DicomSequence sequence)
            {
                int itemIndex = 0;
                foreach (var sequenceItem in sequence.Items)
                {
                    // Create a placeholder for sequence item
                    var sequenceItemTag = new DicomTagItem(
                        item.Tag,
                        "SQ",
                        $"[Item {itemIndex}]",
                        level + 1)
                    {
                        Parent = tagItem,
                        IsSequence = true
                    };

                    // Recursively extract nested tags
                    var nestedTags = ExtractTagsRecursive(sequenceItem, includePrivateTags, level + 2);
                    foreach (var nestedTag in nestedTags)
                    {
                        nestedTag.Parent = sequenceItemTag;
                        sequenceItemTag.Children.Add(nestedTag);
                    }

                    tagItem.Children.Add(sequenceItemTag);
                    itemIndex++;
                }
            }

            yield return tagItem;
        }
    }

    private DicomTagItem CreateTagItem(DicomItem item, DicomDataset dataset, int level)
    {
        var tag = item.Tag;
        var vr = item.ValueRepresentation.Code;
        var value = GetItemValueAsString(item, dataset);

        var tagItem = new DicomTagItem(tag, vr, value, level)
        {
            SourceItem = item
        };

        return tagItem;
    }

    private string GetItemValueAsString(DicomItem item, DicomDataset dataset)
    {
        try
        {
            return item switch
            {
                DicomSequence seq => $"[{seq.Items.Count} item(s)]",
                DicomFragmentSequence frag => $"[{frag.Fragments.Count} fragment(s)]",
                DicomElement element when element.Count == 0 => string.Empty,
                DicomElement element when IsBinaryVR(element.ValueRepresentation.Code) 
                    => GetBinaryValueDisplay(element),
                _ => GetStringValue(item, dataset)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reading value for tag {Tag}", item.Tag);
            return "[Error reading value]";
        }
    }

    /// <summary>
    /// Gets a display string for binary data.
    /// </summary>
    private string GetBinaryValueDisplay(DicomElement element)
    {
        var size = element.Buffer?.Size ?? 0;
        if (size == 0)
            return "[Empty binary data]";
        
        if (size < 1024)
            return $"[Binary: {size} bytes]";
        else if (size < 1024 * 1024)
            return $"[Binary: {size / 1024.0:F1} KB]";
        else
            return $"[Binary: {size / (1024.0 * 1024.0):F1} MB]";
    }

    /// <summary>
    /// Attempts to get the string value from a DICOM item.
    /// </summary>
    private string GetStringValue(DicomItem item, DicomDataset dataset)
    {
        try
        {
            // Try to get the value as a string
            if (dataset.TryGetString(item.Tag, out var value))
            {
                return value ?? string.Empty;
            }

            // For elements that can't be converted to string, try to get raw value
            if (item is DicomElement element && element.Count > 0)
            {
                // Try to get values array
                try
                {
                    var values = element.Get<string[]>();
                    if (values != null && values.Length > 0)
                    {
                        return string.Join("\\", values);
                    }
                }
                catch
                {
                    // Ignore and return empty
                }
            }

            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private bool IsBinaryVR(string vr)
    {
        return vr is "OB" or "OW" or "OF" or "OD" or "OL" or "OV" or "UN";
    }

    public Result UpdateTagValue(DicomDataset dataset, DicomTag tag, string newValue)
    {
        try
        {
            if (!dataset.Contains(tag))
            {
                return Result.Failure($"Tag {tag} not found in dataset");
            }

            var item = dataset.GetDicomItem<DicomItem>(tag);
            var vr = item.ValueRepresentation.Code;

            // Validate the new value
            var validation = _validationService.ValidateValue(tag, vr, newValue);
            if (!validation.IsValid)
            {
                return Result.Failure(validation.ErrorMessage ?? "Validation failed");
            }

            // Update based on VR type
            UpdateDatasetValue(dataset, tag, vr, newValue);

            _logger.LogDebug("Updated tag {Tag} with value: {Value}", tag, newValue);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update tag {Tag}", tag);
            return Result.Failure($"Failed to update tag: {ex.Message}", ex);
        }
    }

    private void UpdateDatasetValue(DicomDataset dataset, DicomTag tag, string vr, string value)
    {
        // Handle different VR types appropriately
        switch (vr)
        {
            case "DA": // Date
                dataset.AddOrUpdate(tag, value);
                break;
            case "TM": // Time
                dataset.AddOrUpdate(tag, value);
                break;
            case "DT": // DateTime
                dataset.AddOrUpdate(tag, value);
                break;
            case "AS": // Age String
                dataset.AddOrUpdate(tag, value);
                break;
            case "DS": // Decimal String
                if (double.TryParse(value, out var dValue))
                    dataset.AddOrUpdate(tag, dValue);
                else
                    dataset.AddOrUpdate(tag, value);
                break;
            case "IS": // Integer String
                if (int.TryParse(value, out var iValue))
                    dataset.AddOrUpdate(tag, iValue);
                else
                    dataset.AddOrUpdate(tag, value);
                break;
            case "SL": // Signed Long
                if (int.TryParse(value, out var slValue))
                    dataset.AddOrUpdate(tag, slValue);
                break;
            case "SS": // Signed Short
                if (short.TryParse(value, out var ssValue))
                    dataset.AddOrUpdate(tag, ssValue);
                break;
            case "UL": // Unsigned Long
                if (uint.TryParse(value, out var ulValue))
                    dataset.AddOrUpdate(tag, ulValue);
                break;
            case "US": // Unsigned Short
                if (ushort.TryParse(value, out var usValue))
                    dataset.AddOrUpdate(tag, usValue);
                break;
            case "FL": // Floating Point Single
                if (float.TryParse(value, out var flValue))
                    dataset.AddOrUpdate(tag, flValue);
                break;
            case "FD": // Floating Point Double
                if (double.TryParse(value, out var fdValue))
                    dataset.AddOrUpdate(tag, fdValue);
                break;
            default:
                // String types (LO, SH, PN, LT, ST, UT, UI, CS, AE, etc.)
                dataset.AddOrUpdate(tag, value);
                break;
        }
    }

    public Result AddTag(DicomDataset dataset, DicomTag tag, string vr, string value)
    {
        try
        {
            if (dataset.Contains(tag))
            {
                // For AddTag used in undo scenarios, allow update
                return UpdateTagValue(dataset, tag, value);
            }

            var validation = _validationService.ValidateValue(tag, vr, value);
            if (!validation.IsValid)
            {
                return Result.Failure(validation.ErrorMessage ?? "Validation failed");
            }

            UpdateDatasetValue(dataset, tag, vr, value);

            _logger.LogDebug("Added tag {Tag} with value: {Value}", tag, value);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add tag {Tag}", tag);
            return Result.Failure($"Failed to add tag: {ex.Message}", ex);
        }
    }

    public Result RemoveTag(DicomDataset dataset, DicomTag tag)
    {
        try
        {
            if (!dataset.Contains(tag))
            {
                return Result.Failure($"Tag {tag} not found in dataset");
            }

            dataset.Remove(tag);
            _logger.LogDebug("Removed tag {Tag}", tag);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove tag {Tag}", tag);
            return Result.Failure($"Failed to remove tag: {ex.Message}", ex);
        }
    }

    public Result CopyTags(DicomDataset source, DicomDataset target, IEnumerable<DicomTag> tags)
    {
        try
        {
            foreach (var tag in tags)
            {
                if (source.Contains(tag))
                {
                    var item = source.GetDicomItem<DicomItem>(tag);
                    target.AddOrUpdate(item);
                }
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy tags");
            return Result.Failure($"Failed to copy tags: {ex.Message}", ex);
        }
    }

    public string? GetTagValue(DicomDataset dataset, DicomTag tag)
    {
        try
        {
            if (!dataset.Contains(tag))
                return null;

            return dataset.TryGetString(tag, out var value) ? value : null;
        }
        catch
        {
            return null;
        }
    }

    public IEnumerable<DicomTagItem> SearchTags(IEnumerable<DicomTagItem> tags, string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return tags;

        var search = searchText.ToLowerInvariant();
        return tags.Where(t =>
            t.TagDisplay.ToLowerInvariant().Contains(search) ||
            t.TagName.ToLowerInvariant().Contains(search) ||
            t.ValueDisplay.ToLowerInvariant().Contains(search) ||
            t.VR.ToLowerInvariant().Contains(search) ||
            (t.PrivateCreator?.ToLowerInvariant().Contains(search) ?? false));
    }

    public IEnumerable<DicomTagItem> FilterTags(IEnumerable<DicomTagItem> tags, TagFilterOptions options)
    {
        var result = tags;

        if (!string.IsNullOrWhiteSpace(options.SearchText))
        {
            result = SearchTags(result, options.SearchText);
        }

        if (options.ShowOnlyWithErrors)
        {
            result = result.Where(t => t.HasValidationError);
        }

        if (options.VRFilter?.Any() == true)
        {
            result = result.Where(t => options.VRFilter.Contains(t.VR));
        }

        if (!string.IsNullOrWhiteSpace(options.GroupFilter))
        {
            if (ushort.TryParse(options.GroupFilter, System.Globalization.NumberStyles.HexNumber, null, out var group))
            {
                result = result.Where(t => t.Tag.Group == group);
            }
        }

        return result;
    }
}
