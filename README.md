# DICOM Editor

A fast, modern .NET 8 WPF application for viewing and editing DICOM medical imaging files. Optimized to efficiently handle 20,000+ files with tag search, inline editing, undo/redo, and safe backup support.

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)
![WPF](https://img.shields.io/badge/UI-WPF-0078D4)
![License](https://img.shields.io/badge/License-Proprietary-red)

## Features

### File Management
- Load individual DICOM files or entire folders
- Drag-and-drop support for files and folders
- Efficiently handles 20,000+ files with UI virtualization
- Progress tracking for large batch operations
- Cancel long-running operations

### Tag Viewing & Editing
- View all standard and private (manufacturer-specific) DICOM tags
- Support for multi-valued tags (backslash-separated values)
- Inline tag value editing with real-time validation
- Visual distinction for private tags, sequences, and empty values
- Search and filter tags by name, value, or tag ID

### Bulk Operations
- Apply a tag value to all loaded files at once
- Delete a tag from all loaded files
- Progress tracking for bulk operations

### Data Safety
- Full undo/redo support for all edit operations
- Optional automatic backup before saving
- Unsaved changes warning on exit

### Validation
- DICOM-compliant validation for all Value Representations (VRs)
- Real-time validation feedback during editing
- Support for multi-valued string validation

## Technology Stack

| Component | Technology |
|-----------|------------|
| Platform | .NET 8, Windows |
| UI Framework | WPF |
| DICOM Library | [fo-dicom](https://github.com/fo-dicom/fo-dicom) 5.2.5 |
| MVVM Framework | [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) 8.4.0 |
| Dependency Injection | Microsoft.Extensions.DependencyInjection |
| Logging | Serilog (file sink) |
| Settings | System.Text.Json |

## Project Structure

```
DicomEditor/
??? Core/                    # Base classes and utilities
?   ??? ViewModelBase.cs     # MVVM base class
?   ??? Result.cs            # Operation result pattern
?   ??? AsyncRelayCommand.cs # Async command implementation
??? Models/                  # Data models
?   ??? DicomFileItem.cs     # DICOM file representation
?   ??? DicomTagItem.cs      # DICOM tag representation
?   ??? EditActions.cs       # Undo/redo action definitions
?   ??? SupportingModels.cs  # Filter options, settings, etc.
??? Services/                # Business logic services
?   ??? Interfaces/          # Service contracts
?   ??? DicomFileService.cs  # File loading/saving
?   ??? DicomTagService.cs   # Tag extraction/modification
?   ??? DicomValidationService.cs # VR validation
?   ??? UndoRedoService.cs   # Undo/redo management
?   ??? SettingsService.cs   # App settings persistence
?   ??? DialogService.cs     # UI dialogs
??? ViewModels/              # MVVM ViewModels
?   ??? MainViewModel.cs     # Main window logic
?   ??? TagEditorViewModel.cs # Tag editing logic
??? Controls/                # Custom WPF controls
?   ??? InputDialog.xaml     # Input dialog control
??? Resources/               # Icons and assets
??? App.xaml                 # Application entry
??? MainWindow.xaml          # Main window UI
```

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 (17.8+) or JetBrains Rider
- Windows 10/11

### Build & Run

1. Clone the repository:
   ```bash
   git clone <repository-url>
   cd DicomEditor
   ```

2. Restore NuGet packages:
   ```bash
   dotnet restore
   ```

3. Build and run:
   ```bash
   dotnet run --project DicomEditor
   ```

   Or open `DicomEditor.sln` in Visual Studio and press **F5**.

## Usage

### Opening Files

| Action | Method |
|--------|--------|
| Open files | **File ? Open Files...** or `Ctrl+O` |
| Open folder | **File ? Open Folder...** or `Ctrl+Shift+O` |
| Drag & drop | Drag files/folders onto the window |

### Editing Tags

1. Select a file from the left panel
2. Select a tag from the tag list
3. Edit using one of these methods:
   - Double-click the value cell
   - Press `F2` to open the edit dialog
   - Type directly in the value cell

### Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl+O` | Open files |
| `Ctrl+Shift+O` | Open folder |
| `Ctrl+S` | Save selected file |
| `Ctrl+Shift+S` | Save all modified files |
| `Ctrl+Z` | Undo |
| `Ctrl+Y` | Redo |
| `Ctrl+C` | Copy selected tag |
| `F2` | Edit selected tag |
| `F5` | Refresh tag list |
| `Delete` | Delete selected tag |
| `Ctrl+Shift+A` | Apply tag value to all files |
| `Ctrl+Shift+D` | Delete tag from all files |

### Bulk Operations

1. Select the tag you want to modify
2. Use **Edit ? Apply Value to All Files...** (`Ctrl+Shift+A`) to set the same value across all loaded files
3. Use **Edit ? Delete Tag from All Files...** (`Ctrl+Shift+D`) to remove a tag from all files

### Saving Changes

- Modified files are highlighted in orange
- Use **File ? Save** (`Ctrl+S`) to save the selected file
- Use **File ? Save All Modified** (`Ctrl+Shift+S`) to save all changes
- Backups are automatically created (configurable in settings)

## Configuration

Settings are stored in:
```
%LOCALAPPDATA%\DicomEditor\settings.json
```

| Setting | Default | Description |
|---------|---------|-------------|
| `CreateBackupOnSave` | `true` | Create backup before saving |
| `BackupFolderName` | `.backup` | Backup folder name |
| `MaxBackupVersions` | `5` | Maximum backup versions to keep |
| `ShowHiddenTags` | `true` | Show private/manufacturer tags |
| `ValidateOnEdit` | `true` | Validate values during editing |

## Logging

Logs are written to:
```
%LOCALAPPDATA%\DicomEditor\logs\
```

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## Acknowledgments

- [fo-dicom](https://github.com/fo-dicom/fo-dicom) - DICOM library for .NET
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) - MVVM toolkit
- [Serilog](https://serilog.net/) - Structured logging
