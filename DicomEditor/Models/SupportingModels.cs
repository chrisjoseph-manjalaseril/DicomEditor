namespace DicomEditor.Models;

/// <summary>
/// Filter options for the DICOM tag list.
/// </summary>
public class TagFilterOptions
{
    public string? SearchText { get; set; }
    public bool ShowOnlyWithErrors { get; set; }
    public List<string>? VRFilter { get; set; }
    public string? GroupFilter { get; set; }
}

/// <summary>
/// Sort options for the file list or tag list.
/// </summary>
public enum SortDirection
{
    Ascending,
    Descending
}

public class SortOptions
{
    public string PropertyName { get; set; } = "FileName";
    public SortDirection Direction { get; set; } = SortDirection.Ascending;
}

/// <summary>
/// Progress information for long-running operations.
/// </summary>
public class ProgressInfo
{
    public int Current { get; set; }
    public int Total { get; set; }
    public string? Message { get; set; }
    public double Percentage => Total > 0 ? (double)Current / Total * 100 : 0;
    public bool IsIndeterminate { get; set; }
}

/// <summary>
/// Application settings that persist between sessions.
/// </summary>
public class AppSettings
{
    public bool CreateBackupOnSave { get; set; } = true;
    public string BackupFolderName { get; set; } = ".backup";
    public int MaxBackupVersions { get; set; } = 5;
    public bool AutoExpandSequences { get; set; } = false;
    public int MaxFilesToLoadConcurrently { get; set; } = 10;
    public List<string> RecentFolders { get; set; } = new();
    public int MaxRecentFolders { get; set; } = 10;
    /// <summary>
    /// Whether to show private tags (manufacturer-specific tags) in the tag list.
    /// Default is true to show all tags for completeness.
    /// </summary>
    public bool ShowHiddenTags { get; set; } = true;
    public bool ValidateOnEdit { get; set; } = true;
    public string? LastOpenedFolder { get; set; }
    public double MainWindowWidth { get; set; } = 1200;
    public double MainWindowHeight { get; set; } = 800;
}
