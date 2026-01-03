using DicomEditor.Models;
using FellowOakDicom;

namespace DicomEditor.Services.Interfaces;

/// <summary>
/// Service interface for DICOM tag operations.
/// Handles reading, editing, and validating DICOM tags.
/// </summary>
public interface IDicomTagService
{
    /// <summary>
    /// Extracts all tags from a DICOM dataset as a flat list.
    /// </summary>
    IEnumerable<DicomTagItem> ExtractTags(DicomDataset dataset, bool includePrivateTags = true);

    /// <summary>
    /// Extracts tags as a hierarchical structure (for sequences).
    /// </summary>
    IEnumerable<DicomTagItem> ExtractTagsHierarchical(DicomDataset dataset, bool includePrivateTags = true);

    /// <summary>
    /// Updates a tag value in the dataset.
    /// </summary>
    Core.Result UpdateTagValue(DicomDataset dataset, DicomTag tag, string newValue);

    /// <summary>
    /// Adds a new tag to the dataset.
    /// </summary>
    Core.Result AddTag(DicomDataset dataset, DicomTag tag, string vr, string value);

    /// <summary>
    /// Removes a tag from the dataset.
    /// </summary>
    Core.Result RemoveTag(DicomDataset dataset, DicomTag tag);

    /// <summary>
    /// Copies tag values from one dataset to another.
    /// </summary>
    Core.Result CopyTags(DicomDataset source, DicomDataset target, IEnumerable<DicomTag> tags);

    /// <summary>
    /// Gets a tag value as a string.
    /// </summary>
    string? GetTagValue(DicomDataset dataset, DicomTag tag);

    /// <summary>
    /// Searches for tags matching the criteria.
    /// </summary>
    IEnumerable<DicomTagItem> SearchTags(IEnumerable<DicomTagItem> tags, string searchText);

    /// <summary>
    /// Filters tags based on filter options.
    /// </summary>
    IEnumerable<DicomTagItem> FilterTags(IEnumerable<DicomTagItem> tags, TagFilterOptions options);
}
