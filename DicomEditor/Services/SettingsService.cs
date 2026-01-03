using System.IO;
using System.Text.Json;
using DicomEditor.Models;
using DicomEditor.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace DicomEditor.Services;

/// <summary>
/// Implementation of ISettingsService.
/// Manages application settings with JSON persistence.
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly ILogger<SettingsService> _logger;
    private readonly string _settingsPath;
    private AppSettings _settings = new();

    public AppSettings Settings => _settings;
    public event EventHandler? SettingsChanged;

    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger;
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appDataPath, "DicomEditor");
        
        if (!Directory.Exists(appFolder))
        {
            Directory.CreateDirectory(appFolder);
        }
        
        _settingsPath = Path.Combine(appFolder, "settings.json");
    }

    public async Task LoadAsync()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = await File.ReadAllTextAsync(_settingsPath);
                _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                _logger.LogInformation("Settings loaded from {Path}", _settingsPath);
            }
            else
            {
                _settings = new AppSettings();
                await SaveAsync(); // Create default settings file
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load settings, using defaults");
            _settings = new AppSettings();
        }
    }

    public async Task SaveAsync()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_settings, options);
            await File.WriteAllTextAsync(_settingsPath, json);
            _logger.LogDebug("Settings saved to {Path}", _settingsPath);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
        }
    }

    public void AddRecentFolder(string folderPath)
    {
        // Remove if already exists
        _settings.RecentFolders.Remove(folderPath);
        
        // Add to beginning
        _settings.RecentFolders.Insert(0, folderPath);
        
        // Trim to max
        while (_settings.RecentFolders.Count > _settings.MaxRecentFolders)
        {
            _settings.RecentFolders.RemoveAt(_settings.RecentFolders.Count - 1);
        }
        
        _settings.LastOpenedFolder = folderPath;
    }
}
