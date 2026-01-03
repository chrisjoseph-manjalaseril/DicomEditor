using DicomEditor.Models;

namespace DicomEditor.Services.Interfaces;

/// <summary>
/// Service interface for DICOM file operations.
/// Handles loading, parsing, and saving DICOM files.
/// </summary>
public interface IDicomFileService
{
    /// <summary>
    /// Loads a single DICOM file asynchronously.
    /// </summary>
    Task<DicomFileItem?> LoadFileAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads multiple DICOM files from paths asynchronously with progress reporting.
    /// Uses parallel loading for performance.
    /// </summary>
    IAsyncEnumerable<DicomFileItem> LoadFilesAsync(
        IEnumerable<string> filePaths,
        IProgress<ProgressInfo>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Discovers all DICOM files in a folder recursively.
    /// </summary>
    Task<IEnumerable<string>> DiscoverDicomFilesAsync(
        string folderPath,
        bool recursive = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a DICOM file with optional backup.
    /// </summary>
    Task<Core.Result> SaveFileAsync(
        DicomFileItem file,
        bool createBackup = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a DICOM file to a new location.
    /// </summary>
    Task<Core.Result> SaveFileAsAsync(
        DicomFileItem file,
        string newPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves multiple files with progress reporting.
    /// </summary>
    Task<Core.Result> SaveFilesAsync(
        IEnumerable<DicomFileItem> files,
        bool createBackup = true,
        IProgress<ProgressInfo>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a file is a valid DICOM file.
    /// </summary>
    Task<bool> IsDicomFileAsync(string filePath, CancellationToken cancellationToken = default);
}
