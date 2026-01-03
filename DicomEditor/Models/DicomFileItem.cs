using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using FellowOakDicom;

namespace DicomEditor.Models;

/// <summary>
/// Represents a DICOM file loaded into the application.
/// Tracks file state and provides metadata access.
/// </summary>
public partial class DicomFileItem : ObservableObject
{
    private readonly DicomFile _dicomFile;

    [ObservableProperty]
    private string _filePath;

    [ObservableProperty]
    private string _fileName;

    [ObservableProperty]
    private bool _isModified;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _hasErrors;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private DateTime _lastModified;

    [ObservableProperty]
    private long _fileSize;

    // Common DICOM metadata properties for quick display
    [ObservableProperty]
    private string? _patientName;

    [ObservableProperty]
    private string? _patientId;

    [ObservableProperty]
    private string? _studyDescription;

    [ObservableProperty]
    private string? _seriesDescription;

    [ObservableProperty]
    private string? _modality;

    [ObservableProperty]
    private string? _studyDate;

    [ObservableProperty]
    private string? _sopInstanceUid;

    public DicomDataset Dataset => _dicomFile.Dataset;
    public DicomFile DicomFile => _dicomFile;

    public DicomFileItem(DicomFile dicomFile, string filePath)
    {
        _dicomFile = dicomFile ?? throw new ArgumentNullException(nameof(dicomFile));
        _filePath = filePath;
        _fileName = Path.GetFileName(filePath);

        var fileInfo = new FileInfo(filePath);
        _lastModified = fileInfo.LastWriteTime;
        _fileSize = fileInfo.Length;

        ExtractCommonMetadata();
    }

    /// <summary>
    /// Extracts commonly used DICOM tags for display in file list.
    /// </summary>
    private void ExtractCommonMetadata()
    {
        try
        {
            PatientName = GetTagValueOrDefault(DicomTag.PatientName);
            PatientId = GetTagValueOrDefault(DicomTag.PatientID);
            StudyDescription = GetTagValueOrDefault(DicomTag.StudyDescription);
            SeriesDescription = GetTagValueOrDefault(DicomTag.SeriesDescription);
            Modality = GetTagValueOrDefault(DicomTag.Modality);
            StudyDate = GetTagValueOrDefault(DicomTag.StudyDate);
            SopInstanceUid = GetTagValueOrDefault(DicomTag.SOPInstanceUID);
        }
        catch (Exception ex)
        {
            HasErrors = true;
            ErrorMessage = $"Error extracting metadata: {ex.Message}";
        }
    }

    private string? GetTagValueOrDefault(DicomTag tag)
    {
        return Dataset.TryGetString(tag, out var value) ? value : null;
    }

    /// <summary>
    /// Marks the file as modified.
    /// </summary>
    public void MarkModified()
    {
        IsModified = true;
    }

    /// <summary>
    /// Clears the modified flag (after save).
    /// </summary>
    public void ClearModified()
    {
        IsModified = false;
    }

    /// <summary>
    /// Refreshes metadata after tag changes.
    /// </summary>
    public void RefreshMetadata()
    {
        ExtractCommonMetadata();
    }
}
