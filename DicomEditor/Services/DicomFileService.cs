using System.Collections.Concurrent;
using System.IO;
using System.Runtime.CompilerServices;
using DicomEditor.Core;
using DicomEditor.Models;
using DicomEditor.Services.Interfaces;
using FellowOakDicom;
using Microsoft.Extensions.Logging;

namespace DicomEditor.Services;

/// <summary>
/// Implementation of IDicomFileService.
/// Handles all DICOM file I/O operations with performance optimization for large file sets.
/// Uses parallel processing and streaming for efficient memory usage.
/// </summary>
public class DicomFileService : IDicomFileService
{
    private readonly ILogger<DicomFileService> _logger;
    private readonly ISettingsService _settingsService;
    private static readonly string[] DicomExtensions = { ".dcm", ".dicom", ".dic", "" };

    public DicomFileService(ILogger<DicomFileService> logger, ISettingsService settingsService)
    {
        _logger = logger;
        _settingsService = settingsService;
    }

    public async Task<DicomFileItem?> LoadFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Load DICOM file asynchronously
            var dicomFile = await DicomFile.OpenAsync(filePath);
            return new DicomFileItem(dicomFile, filePath);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load DICOM file: {FilePath}", filePath);
            return null;
        }
    }

    public async IAsyncEnumerable<DicomFileItem> LoadFilesAsync(
        IEnumerable<string> filePaths,
        IProgress<ProgressInfo>? progress = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var pathList = filePaths.ToList();
        var total = pathList.Count;
        var processed = 0;
        var maxConcurrency = _settingsService.Settings.MaxFilesToLoadConcurrently;

        // Use semaphore to limit concurrent file operations
        using var semaphore = new SemaphoreSlim(maxConcurrency);
        var results = new ConcurrentQueue<DicomFileItem>();
        var tasks = new List<Task>();

        foreach (var batch in pathList.Chunk(maxConcurrency * 2))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batchTasks = batch.Select(async path =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var item = await LoadFileAsync(path, cancellationToken);
                    if (item != null)
                    {
                        results.Enqueue(item);
                    }
                }
                finally
                {
                    semaphore.Release();
                    Interlocked.Increment(ref processed);
                    progress?.Report(new ProgressInfo
                    {
                        Current = processed,
                        Total = total,
                        Message = $"Loading file {processed} of {total}..."
                    });
                }
            });

            await Task.WhenAll(batchTasks);

            // Yield results as they become available
            while (results.TryDequeue(out var item))
            {
                yield return item;
            }
        }
    }

    public async Task<IEnumerable<string>> DiscoverDicomFilesAsync(
        string folderPath,
        bool recursive = true,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var allFiles = new List<string>();

            try
            {
                // Get all files and filter by extension
                var files = Directory.EnumerateFiles(folderPath, "*.*", searchOption)
                    .Where(f => IsPotentialDicomFile(f));

                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    allFiles.Add(file);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Access denied to folder: {FolderPath}", folderPath);
            }
            catch (DirectoryNotFoundException ex)
            {
                _logger.LogWarning(ex, "Directory not found: {FolderPath}", folderPath);
            }

            return allFiles;
        }, cancellationToken);
    }

    public async Task<Result> SaveFileAsync(
        DicomFileItem file,
        bool createBackup = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (createBackup && _settingsService.Settings.CreateBackupOnSave)
            {
                await CreateBackupAsync(file.FilePath, cancellationToken);
            }

            await file.DicomFile.SaveAsync(file.FilePath);
            file.ClearModified();
            file.RefreshMetadata();

            _logger.LogInformation("Saved DICOM file: {FilePath}", file.FilePath);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save DICOM file: {FilePath}", file.FilePath);
            return Result.Failure($"Failed to save file: {ex.Message}", ex);
        }
    }

    public async Task<Result> SaveFileAsAsync(
        DicomFileItem file,
        string newPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Ensure directory exists
            var directory = Path.GetDirectoryName(newPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await file.DicomFile.SaveAsync(newPath);
            _logger.LogInformation("Saved DICOM file as: {NewPath}", newPath);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save DICOM file as: {NewPath}", newPath);
            return Result.Failure($"Failed to save file: {ex.Message}", ex);
        }
    }

    public async Task<Result> SaveFilesAsync(
        IEnumerable<DicomFileItem> files,
        bool createBackup = true,
        IProgress<ProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var fileList = files.ToList();
        var total = fileList.Count;
        var processed = 0;
        var errors = new List<string>();

        foreach (var file in fileList)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await SaveFileAsync(file, createBackup, cancellationToken);
            if (!result.IsSuccess)
            {
                errors.Add($"{file.FileName}: {result.ErrorMessage}");
            }

            processed++;
            progress?.Report(new ProgressInfo
            {
                Current = processed,
                Total = total,
                Message = $"Saving file {processed} of {total}..."
            });
        }

        if (errors.Any())
        {
            return Result.Failure($"Failed to save {errors.Count} files:\n{string.Join("\n", errors)}");
        }

        return Result.Success();
    }

    public async Task<bool> IsDicomFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            // Quick check for DICOM preamble
            return await Task.Run(() =>
            {
                using var stream = File.OpenRead(filePath);
                if (stream.Length < 132) return false;

                stream.Seek(128, SeekOrigin.Begin);
                var buffer = new byte[4];
                stream.Read(buffer, 0, 4);

                // Check for "DICM" magic number
                return buffer[0] == 'D' && buffer[1] == 'I' && buffer[2] == 'C' && buffer[3] == 'M';
            }, cancellationToken);
        }
        catch
        {
            return false;
        }
    }

    private bool IsPotentialDicomFile(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return DicomExtensions.Contains(ext);
    }

    private async Task CreateBackupAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            var fileName = Path.GetFileName(filePath);
            var backupFolder = Path.Combine(directory!, _settingsService.Settings.BackupFolderName);

            if (!Directory.Exists(backupFolder))
            {
                Directory.CreateDirectory(backupFolder);
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupPath = Path.Combine(backupFolder, $"{fileName}.{timestamp}.bak");

            await Task.Run(() => File.Copy(filePath, backupPath, true), cancellationToken);

            // Clean up old backups
            await CleanupOldBackupsAsync(backupFolder, fileName, cancellationToken);

            _logger.LogDebug("Created backup: {BackupPath}", backupPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create backup for: {FilePath}", filePath);
        }
    }

    private async Task CleanupOldBackupsAsync(string backupFolder, string originalFileName, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            var backups = Directory.GetFiles(backupFolder, $"{originalFileName}.*.bak")
                .OrderByDescending(f => File.GetCreationTime(f))
                .Skip(_settingsService.Settings.MaxBackupVersions)
                .ToList();

            foreach (var backup in backups)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    File.Delete(backup);
                    _logger.LogDebug("Deleted old backup: {Backup}", backup);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete old backup: {Backup}", backup);
                }
            }
        }, cancellationToken);
    }
}
