using System.Windows;
using DicomEditor.Services;
using DicomEditor.Services.Interfaces;
using DicomEditor.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace DicomEditor;

/// <summary>
/// Application entry point with dependency injection setup.
/// Uses Microsoft.Extensions.Hosting for DI container management.
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    /// <summary>
    /// Gets the service provider for resolving dependencies.
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Configure Serilog for file logging
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DicomEditor", "logs", "log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

        // Build the host with DI
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Register services (singletons for shared state)
                services.AddSingleton<IDialogService, DialogService>();
                services.AddSingleton<ISettingsService, SettingsService>();
                services.AddSingleton<IDicomFileService, DicomFileService>();
                services.AddSingleton<IDicomTagService, DicomTagService>();
                services.AddSingleton<IDicomValidationService, DicomValidationService>();
                services.AddSingleton<IUndoRedoService, UndoRedoService>();

                // Register ViewModels
                services.AddSingleton<MainViewModel>();
                services.AddTransient<TagEditorViewModel>();

                // Register MainWindow
                services.AddSingleton<MainWindow>();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddSerilog(Log.Logger);
            })
            .Build();

        Services = _host.Services;

        await _host.StartAsync();

        // Load settings before showing window
        var settingsService = Services.GetRequiredService<ISettingsService>();
        await settingsService.LoadAsync();

        // Create and show main window with ViewModel
        var mainWindow = Services.GetRequiredService<MainWindow>();
        var mainViewModel = Services.GetRequiredService<MainViewModel>();
        mainWindow.DataContext = mainViewModel;

        // Apply saved window size
        var settings = settingsService.Settings;
        if (settings.MainWindowWidth > 0)
            mainWindow.Width = settings.MainWindowWidth;
        if (settings.MainWindowHeight > 0)
            mainWindow.Height = settings.MainWindowHeight;

        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        // Save settings on exit
        if (_host != null)
        {
            var settingsService = Services.GetService<ISettingsService>();
            if (settingsService != null && MainWindow != null)
            {
                settingsService.Settings.MainWindowWidth = MainWindow.Width;
                settingsService.Settings.MainWindowHeight = MainWindow.Height;
                await settingsService.SaveAsync();
            }

            await _host.StopAsync(TimeSpan.FromSeconds(5));
            _host.Dispose();
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
