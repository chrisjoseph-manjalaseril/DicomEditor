using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DicomEditor.Core;
using DicomEditor.Models;
using DicomEditor.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Data;

namespace DicomEditor.ViewModels;

/// <summary>
/// Main ViewModel for the DICOM Editor application.
/// Manages file loading, tag display, and coordinates between sub-ViewModels.
/// Uses virtualization-friendly observable collections for performance with large datasets.
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly IDicomFileService _fileService;
    private readonly IDicomTagService _tagService;
    private readonly IDicomValidationService _validationService;
    private readonly IUndoRedoService _undoRedoService;
    private readonly ISettingsService _settingsService;
    private readonly IDialogService _dialogService;
    private readonly ILogger<MainViewModel> _logger;

    private CancellationTokenSource? _loadCts;

    #region Observable Properties

    [ObservableProperty]
    private string _title = "DICOM Editor";

    [ObservableProperty]
    private string? _statusMessage = "Ready";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFiles))]
    [NotifyPropertyChangedFor(nameof(HasModifiedFiles))]
    [NotifyPropertyChangedFor(nameof(ModifiedFilesCount))]
    private ObservableCollection<DicomFileItem> _files = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedFile))]
    [NotifyCanExecuteChangedFor(nameof(SaveSelectedFileCommand))]
    private DicomFileItem? _selectedFile;

    [ObservableProperty]
    private ObservableCollection<DicomTagItem> _tags = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditTagCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteTagCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyTagCommand))]
    [NotifyCanExecuteChangedFor(nameof(BulkUpdateTagCommand))]
    [NotifyCanExecuteChangedFor(nameof(BulkDeleteTagCommand))]
    private DicomTagItem? _selectedTag;

    [ObservableProperty]
    private string? _searchText;

    [ObservableProperty]
    private int _totalFilesCount;

    [ObservableProperty]
    private int _loadedFilesCount;

    [ObservableProperty]
    private double _loadProgress;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _loadingMessage;

    #endregion

    #region Computed Properties

    public bool HasFiles => Files.Count > 0;
    public bool HasSelectedFile => SelectedFile != null;
    public bool HasModifiedFiles => Files.Any(f => f.IsModified);
    public int ModifiedFilesCount => Files.Count(f => f.IsModified);
    public bool CanUndo => _undoRedoService.CanUndo;
    public bool CanRedo => _undoRedoService.CanRedo;
    public string? UndoDescription => _undoRedoService.UndoDescription;
    public string? RedoDescription => _undoRedoService.RedoDescription;

    public ICollectionView? TagsView { get; private set; }

    #endregion

    public MainViewModel(
        IDicomFileService fileService,
        IDicomTagService tagService,
        IDicomValidationService validationService,
        IUndoRedoService undoRedoService,
        ISettingsService settingsService,
        IDialogService dialogService,
        ILogger<MainViewModel> logger)
    {
        _fileService = fileService;
        _tagService = tagService;
        _validationService = validationService;
        _undoRedoService = undoRedoService;
        _settingsService = settingsService;
        _dialogService = dialogService;
        _logger = logger;

        _undoRedoService.StateChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            OnPropertyChanged(nameof(UndoDescription));
            OnPropertyChanged(nameof(RedoDescription));
        };

        // Subscribe to Files collection changes
        Files.CollectionChanged += OnFilesCollectionChanged;

        SetupTagsView();
    }

    private void OnFilesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Unsubscribe from old items
        if (e.OldItems != null)
        {
            foreach (DicomFileItem item in e.OldItems)
            {
                item.PropertyChanged -= OnFileItemPropertyChanged;
            }
        }

        // Subscribe to new items
        if (e.NewItems != null)
        {
            foreach (DicomFileItem item in e.NewItems)
            {
                item.PropertyChanged += OnFileItemPropertyChanged;
            }
        }

        // Reset - subscribe/unsubscribe all
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var item in Files)
            {
                item.PropertyChanged -= OnFileItemPropertyChanged;
                item.PropertyChanged += OnFileItemPropertyChanged;
            }
        }

        NotifyModifiedFilesChanged();
    }

    private void OnFileItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DicomFileItem.IsModified))
        {
            NotifyModifiedFilesChanged();
        }
    }

    private void NotifyModifiedFilesChanged()
    {
        OnPropertyChanged(nameof(HasModifiedFiles));
        OnPropertyChanged(nameof(ModifiedFilesCount));
    }

    private void SetupTagsView()
    {
        TagsView = CollectionViewSource.GetDefaultView(Tags);
        TagsView.Filter = FilterTag;
    }

    private bool FilterTag(object obj)
    {
        if (obj is not DicomTagItem tag) return false;

        if (string.IsNullOrWhiteSpace(SearchText))
            return true;

        var search = SearchText.ToLowerInvariant();
        return tag.TagDisplay.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               tag.TagName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               tag.ValueDisplay.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    partial void OnSearchTextChanged(string? value) => TagsView?.Refresh();

    partial void OnSelectedFileChanged(DicomFileItem? value)
    {
        if (value != null)
            LoadTagsForFile(value);
        else
        {
            Tags.Clear();
            SelectedTag = null;
        }
    }

    private void LoadTagsForFile(DicomFileItem file)
    {
        // Remember the currently selected tag by its Tag identifier
        var previousSelectedTagId = SelectedTag?.Tag;
        
        // Clear the selected tag first to avoid issues
        SelectedTag = null;
        Tags.Clear();

        try
        {
            var tags = _tagService.ExtractTags(file.Dataset, _settingsService.Settings.ShowHiddenTags);
            foreach (var tag in tags)
            {
                Tags.Add(tag);
            }

            // Try to restore the previously selected tag
            if (previousSelectedTagId != null)
            {
                SelectedTag = Tags.FirstOrDefault(t => t.Tag == previousSelectedTagId);
            }

            StatusMessage = $"Loaded {Tags.Count} tags from {file.FileName}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load tags for file: {FilePath}", file.FilePath);
            StatusMessage = $"Error loading tags: {ex.Message}";
        }
    }

    #region Commands

    [RelayCommand]
    private async Task OpenFilesAsync()
    {
        var files = _dialogService.ShowOpenFileDialog(
            "Open DICOM Files",
            "DICOM Files (*.dcm;*.dicom)|*.dcm;*.dicom|All Files (*.*)|*.*",
            multiSelect: true);

        if (files == null || files.Length == 0) return;

        await LoadFilesAsync(files);
    }

    [RelayCommand]
    private async Task OpenFolderAsync()
    {
        var folder = _dialogService.ShowFolderBrowserDialog(
            "Select DICOM Folder",
            _settingsService.Settings.LastOpenedFolder);

        if (string.IsNullOrEmpty(folder)) return;

        _settingsService.AddRecentFolder(folder);
        await _settingsService.SaveAsync();

        await LoadFolderAsync(folder);
    }

    private async Task LoadFilesAsync(IEnumerable<string> filePaths)
    {
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;

        IsLoading = true;
        LoadingMessage = "Loading DICOM files...";
        
        // Clear selection first
        SelectedFile = null;
        SelectedTag = null;
        Files.Clear();
        Tags.Clear();
        _undoRedoService.Clear();

        var pathList = filePaths.ToList();
        TotalFilesCount = pathList.Count;
        LoadedFilesCount = 0;

        try
        {
            var progress = new Progress<ProgressInfo>(p =>
            {
                LoadedFilesCount = p.Current;
                LoadProgress = p.Percentage;
                LoadingMessage = p.Message;
            });

            await foreach (var file in _fileService.LoadFilesAsync(pathList, progress, token))
            {
                Files.Add(file);
            }

            StatusMessage = $"Loaded {Files.Count} files";
            _logger.LogInformation("Loaded {Count} DICOM files", Files.Count);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Loading cancelled";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading files");
            StatusMessage = $"Error: {ex.Message}";
            _dialogService.ShowError($"Error loading files: {ex.Message}", "Load Error");
        }
        finally
        {
            IsLoading = false;
            LoadingMessage = null;
        }
    }

    private async Task LoadFolderAsync(string folderPath)
    {
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();

        IsLoading = true;
        LoadingMessage = "Discovering DICOM files...";

        try
        {
            var files = await _fileService.DiscoverDicomFilesAsync(folderPath, true, _loadCts.Token);
            var fileList = files.ToList();

            StatusMessage = $"Found {fileList.Count} potential DICOM files";

            if (fileList.Count > 0)
            {
                await LoadFilesAsync(fileList);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering files in folder: {Folder}", folderPath);
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void CancelLoading()
    {
        _loadCts?.Cancel();
        StatusMessage = "Operation cancelled";
    }

    [RelayCommand(CanExecute = nameof(HasSelectedFile))]
    private async Task SaveSelectedFileAsync()
    {
        if (SelectedFile == null) return;

        var result = await _fileService.SaveFileAsync(SelectedFile, _settingsService.Settings.CreateBackupOnSave);
        if (result.IsSuccess)
        {
            StatusMessage = $"Saved {SelectedFile.FileName}";
            SelectedFile.ClearModified();
            NotifyModifiedFilesChanged();
        }
        else
        {
            _dialogService.ShowError(result.ErrorMessage ?? "Failed to save file", "Save Error");
        }
    }

    [RelayCommand]
    private async Task SaveAllModifiedAsync()
    {
        var modifiedFiles = Files.Where(f => f.IsModified).ToList();
        if (modifiedFiles.Count == 0)
        {
            _dialogService.ShowInfo("No modified files to save.", "Save All");
            return;
        }

        if (!_dialogService.ShowConfirmation($"Save {modifiedFiles.Count} modified file(s)?", "Confirm Save"))
            return;

        IsLoading = true;
        LoadingMessage = "Saving files...";
        TotalFilesCount = modifiedFiles.Count;
        LoadedFilesCount = 0;

        try
        {
            var progress = new Progress<ProgressInfo>(p =>
            {
                LoadedFilesCount = p.Current;
                LoadProgress = p.Percentage;
                LoadingMessage = p.Message;
            });

            var result = await _fileService.SaveFilesAsync(modifiedFiles, _settingsService.Settings.CreateBackupOnSave, progress);

            if (result.IsSuccess)
            {
                foreach (var file in modifiedFiles)
                {
                    file.ClearModified();
                }
                NotifyModifiedFilesChanged();
                StatusMessage = $"Saved {modifiedFiles.Count} files";
            }
            else
                _dialogService.ShowError(result.ErrorMessage ?? "Failed to save some files", "Save Error");
        }
        finally
        {
            IsLoading = false;
            LoadingMessage = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditTag))]
    private void EditTag()
    {
        if (SelectedTag == null || !SelectedTag.IsEditable) return;

        var newValue = _dialogService.ShowInputDialog(
            "Edit Tag Value",
            $"Enter new value for {SelectedTag.TagName}:",
            SelectedTag.ValueDisplay);

        if (newValue != null && newValue != SelectedTag.ValueDisplay)
        {
            OnTagValueEdited(SelectedTag, SelectedTag.ValueDisplay, newValue);
        }
    }

    private bool CanEditTag() => SelectedTag?.IsEditable == true;

    [RelayCommand(CanExecute = nameof(CanBulkUpdateTag))]
    private async Task BulkUpdateTagAsync()
    {
        if (SelectedTag == null || Files.Count == 0) return;

        // Store the tag info before any operations
        var tagToUpdate = SelectedTag.Tag;
        var tagName = SelectedTag.TagName;
        var currentValue = SelectedTag.ValueDisplay;

        var newValue = _dialogService.ShowInputDialog(
            "Apply Value to All Files",
            $"Enter new value for '{tagName}' to apply to all {Files.Count} files:",
            currentValue);

        if (newValue == null) return;

        if (!_dialogService.ShowConfirmation(
            $"This will update tag '{tagName}' in {Files.Count} files.\n\nNew value: {newValue}\n\nDo you want to continue?",
            "Confirm Bulk Update"))
            return;

        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;

        IsLoading = true;
        LoadingMessage = $"Updating {tagName} in all files...";
        TotalFilesCount = Files.Count;
        LoadedFilesCount = 0;
        LoadProgress = 0;

        var changes = new List<BulkFileChange>();
        var errors = new List<string>();

        try
        {
            _undoRedoService.BeginBatch($"Bulk update {tagName} in {Files.Count} files");

            await Task.Run(async () =>
            {
                foreach (var file in Files)
                {
                    token.ThrowIfCancellationRequested();

                    try
                    {
                        var oldValue = _tagService.GetTagValue(file.Dataset, tagToUpdate) ?? string.Empty;

                        if (oldValue == newValue)
                            continue;

                        var result = _tagService.UpdateTagValue(file.Dataset, tagToUpdate, newValue);
                        if (result.IsSuccess)
                        {
                            changes.Add(new BulkFileChange(file, tagToUpdate, oldValue, newValue));

                            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                file.MarkModified();
                                file.RefreshMetadata();
                            });
                        }
                        else
                        {
                            errors.Add($"{file.FileName}: {result.ErrorMessage}");
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{file.FileName}: {ex.Message}");
                    }

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        LoadedFilesCount++;
                        LoadProgress = (double)LoadedFilesCount / TotalFilesCount * 100;
                    });
                }
            }, token);

            if (changes.Count > 0)
            {
                var bulkAction = new BulkFileTagChangeAction(
                    $"Bulk update {tagName} in {changes.Count} files",
                    changes,
                    _tagService);

                _undoRedoService.Execute(new AlreadyExecutedAction(bulkAction));
            }

            _undoRedoService.EndBatch();

            // Refresh the current file's tags if selected
            if (SelectedFile != null)
            {
                LoadTagsForFile(SelectedFile);
            }

            // Notify that modified files count changed
            NotifyModifiedFilesChanged();

            if (errors.Count > 0)
            {
                StatusMessage = $"Updated {changes.Count} files with {errors.Count} errors";
                _dialogService.ShowError(
                    $"Updated {changes.Count} files successfully.\n\n{errors.Count} files had errors:\n" +
                    string.Join("\n", errors.Take(10)) +
                    (errors.Count > 10 ? $"\n... and {errors.Count - 10} more" : ""),
                    "Bulk Update Complete");
            }
            else
            {
                StatusMessage = $"Updated {changes.Count} files successfully";
                _dialogService.ShowInfo($"Successfully updated '{tagName}' in {changes.Count} files.", "Bulk Update Complete");
            }

            _logger.LogInformation("Bulk updated tag {TagName} in {Count} files", tagName, changes.Count);
        }
        catch (OperationCanceledException)
        {
            _undoRedoService.EndBatch();
            StatusMessage = "Bulk update cancelled";
            _logger.LogInformation("Bulk update cancelled after {Count} files", LoadedFilesCount);
        }
        catch (Exception ex)
        {
            _undoRedoService.EndBatch();
            _logger.LogError(ex, "Error during bulk update");
            StatusMessage = $"Error: {ex.Message}";
            _dialogService.ShowError($"Error during bulk update: {ex.Message}", "Bulk Update Error");
        }
        finally
        {
            IsLoading = false;
            LoadingMessage = null;
        }
    }

    private bool CanBulkUpdateTag() => SelectedTag?.IsEditable == true && Files.Count > 0;

    [RelayCommand(CanExecute = nameof(HasSelectedTag))]
    private void DeleteTag()
    {
        if (SelectedTag == null || SelectedFile == null) return;

        if (!_dialogService.ShowConfirmation($"Delete tag {SelectedTag.TagName}?", "Confirm Delete"))
            return;

        var tagToDelete = SelectedTag;
        var currentFile = SelectedFile;
        var tagValue = tagToDelete.ValueDisplay;
        var tagVr = tagToDelete.VR;
        var dicomTag = tagToDelete.Tag;

        var action = new TagDeleteAction(
            tagToDelete,
            tag =>
            {
                _tagService.RemoveTag(currentFile.Dataset, tag.Tag);
                Tags.Remove(tag);
                currentFile.MarkModified();
                NotifyModifiedFilesChanged();
            },
            tag =>
            {
                // Restore the tag with its original value
                _tagService.AddTag(currentFile.Dataset, dicomTag, tagVr, tagValue);
                currentFile.MarkModified();
                NotifyModifiedFilesChanged();
            });

        _undoRedoService.Execute(action);
        SelectedTag = null;
        StatusMessage = $"Deleted tag {tagToDelete.TagName}";
    }

    /// <summary>
    /// Bulk delete the selected tag from all loaded files.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanBulkDeleteTag))]
    private async Task BulkDeleteTagAsync()
    {
        if (SelectedTag == null || Files.Count == 0) return;

        // Store tag info before any operations
        var tagToDelete = SelectedTag.Tag;
        var tagName = SelectedTag.TagName;
        var tagVr = SelectedTag.VR;

        // Confirm the bulk delete operation
        if (!_dialogService.ShowConfirmation(
            $"This will DELETE tag '{tagName}' from ALL {Files.Count} loaded files.\n\n" +
            "This action can be undone.\n\n" +
            "Do you want to continue?",
            "Confirm Bulk Delete"))
            return;

        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;

        IsLoading = true;
        LoadingMessage = $"Deleting {tagName} from all files...";
        TotalFilesCount = Files.Count;
        LoadedFilesCount = 0;
        LoadProgress = 0;

        var deletions = new List<BulkFileTagDeletion>();
        var errors = new List<string>();
        var skipped = 0;

        try
        {
            await Task.Run(async () =>
            {
                foreach (var file in Files)
                {
                    token.ThrowIfCancellationRequested();

                    try
                    {
                        // Get the current value before deleting (for undo)
                        var currentValue = _tagService.GetTagValue(file.Dataset, tagToDelete);

                        if (currentValue == null)
                        {
                            // Tag doesn't exist in this file, skip it
                            skipped++;
                        }
                        else
                        {
                            var result = _tagService.RemoveTag(file.Dataset, tagToDelete);
                            if (result.IsSuccess)
                            {
                                deletions.Add(new BulkFileTagDeletion(file, tagToDelete, tagVr, currentValue));

                                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                                {
                                    file.MarkModified();
                                    file.RefreshMetadata();
                                });
                            }
                            else
                            {
                                errors.Add($"{file.FileName}: {result.ErrorMessage}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{file.FileName}: {ex.Message}");
                    }

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        LoadedFilesCount++;
                        LoadProgress = (double)LoadedFilesCount / TotalFilesCount * 100;
                    });
                }
            }, token);

            // Create the bulk action for undo/redo
            if (deletions.Count > 0)
            {
                var bulkAction = new BulkTagDeleteAction(
                    $"Bulk delete {tagName} from {deletions.Count} files",
                    deletions,
                    _tagService);

                _undoRedoService.Execute(new AlreadyExecutedAction(bulkAction));
            }

            // Refresh the current file's tags
            if (SelectedFile != null)
            {
                LoadTagsForFile(SelectedFile);
            }

            // Notify that modified files count changed
            NotifyModifiedFilesChanged();

            // Show result message
            var message = $"Deleted tag '{tagName}' from {deletions.Count} files.";
            if (skipped > 0)
            {
                message += $"\n{skipped} files did not have this tag.";
            }

            if (errors.Count > 0)
            {
                StatusMessage = $"Deleted from {deletions.Count} files with {errors.Count} errors";
                _dialogService.ShowError(
                    message + $"\n\n{errors.Count} files had errors:\n" +
                    string.Join("\n", errors.Take(10)) +
                    (errors.Count > 10 ? $"\n... and {errors.Count - 10} more" : ""),
                    "Bulk Delete Complete");
            }
            else
            {
                StatusMessage = $"Deleted tag from {deletions.Count} files";
                _dialogService.ShowInfo(message, "Bulk Delete Complete");
            }

            _logger.LogInformation("Bulk deleted tag {TagName} from {Count} files", tagName, deletions.Count);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Bulk delete cancelled";
            _logger.LogInformation("Bulk delete cancelled after {Count} files", LoadedFilesCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bulk delete");
            StatusMessage = $"Error: {ex.Message}";
            _dialogService.ShowError($"Error during bulk delete: {ex.Message}", "Bulk Delete Error");
        }
        finally
        {
            IsLoading = false;
            LoadingMessage = null;
        }
    }

    private bool CanBulkDeleteTag() => SelectedTag != null && Files.Count > 0;

    private bool HasSelectedTag() => SelectedTag != null;

    [RelayCommand(CanExecute = nameof(HasSelectedTag))]
    private void CopyTag()
    {
        if (SelectedTag == null) return;

        var text = $"{SelectedTag.TagDisplay}\t{SelectedTag.TagName}\t{SelectedTag.VR}\t{SelectedTag.ValueDisplay}";
        System.Windows.Clipboard.SetText(text);
        StatusMessage = $"Copied tag {SelectedTag.TagName} to clipboard";
    }

    [RelayCommand]
    private void Undo()
    {
        if (!CanUndo) return;
        _undoRedoService.Undo();
        
        if (SelectedFile != null)
            LoadTagsForFile(SelectedFile);
        
        TagsView?.Refresh();
        NotifyModifiedFilesChanged();
        StatusMessage = "Undid last action";
    }

    [RelayCommand]
    private void Redo()
    {
        if (!CanRedo) return;
        _undoRedoService.Redo();
        
        if (SelectedFile != null)
            LoadTagsForFile(SelectedFile);
        
        TagsView?.Refresh();
        NotifyModifiedFilesChanged();
        StatusMessage = "Redid action";
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = null;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (SelectedFile != null)
        {
            LoadTagsForFile(SelectedFile);
        }
    }

    #endregion

    public void OnTagValueEdited(DicomTagItem tag, string oldValue, string newValue)
    {
        if (SelectedFile == null || oldValue == newValue) return;

        var validation = _validationService.ValidateValue(tag.Tag, tag.VR, newValue);
        if (!validation.IsValid)
        {
            tag.HasValidationError = true;
            tag.ValidationErrorMessage = validation.ErrorMessage;
        }
        else
        {
            tag.HasValidationError = false;
            tag.ValidationErrorMessage = null;
        }

        var result = _tagService.UpdateTagValue(SelectedFile.Dataset, tag.Tag, newValue);
        if (result.IsSuccess)
        {
            tag.UpdateValue(newValue);

            var currentFile = SelectedFile;
            var action = new TagValueChangeAction(
                tag, oldValue, newValue,
                () =>
                {
                    currentFile.MarkModified();
                    NotifyModifiedFilesChanged();
                },
                () =>
                {
                    currentFile.MarkModified();
                    NotifyModifiedFilesChanged();
                });

            _undoRedoService.Execute(new AlreadyExecutedAction(action));

            SelectedFile.MarkModified();
            SelectedFile.RefreshMetadata();
            NotifyModifiedFilesChanged();
            StatusMessage = $"Updated {tag.TagName}";
        }
        else
        {
            tag.UpdateValue(oldValue);
            _dialogService.ShowError(result.ErrorMessage ?? "Failed to update tag", "Edit Error");
        }
    }

    private class AlreadyExecutedAction : IEditAction
    {
        private readonly IEditAction _inner;

        public string Description => _inner.Description;

        public AlreadyExecutedAction(IEditAction inner) => _inner = inner;

        public void Execute() { }
        public void Undo() => _inner.Undo();
    }
}