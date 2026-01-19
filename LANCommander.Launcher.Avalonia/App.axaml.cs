using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using LANCommander.Launcher.Avalonia.ViewModels;
using LANCommander.Launcher.Avalonia.Views;
using LANCommander.Launcher.Services.Extensions;
using LANCommander.SDK;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Providers;
using LANCommander.SDK.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Avalonia;

public partial class App : Application
{
    public static IServiceProvider? Services { get; private set; }
    private static ILogger<App>? _logger;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        try
        {
            // Configure services
            var services = new ServiceCollection();
            ConfigureServices(services);
            Services = services.BuildServiceProvider();
            
            _logger = Services.GetRequiredService<ILogger<App>>();
            _logger.LogInformation("LANCommander Avalonia Launcher starting...");

            // Remove Avalonia's built-in data validation plugin to avoid duplicate validations
            var dataValidationPlugins = BindingPlugins.DataValidators;
            for (var i = dataValidationPlugins.Count - 1; i >= 0; i--)
            {
                if (dataValidationPlugins[i] is DataAnnotationsValidationPlugin)
                    dataValidationPlugins.RemoveAt(i);
            }

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
                
                var mainViewModel = Services.GetRequiredService<MainWindowViewModel>();
                
                var mainWindow = new MainWindow
                {
                    DataContext = mainViewModel
                };
                
                mainWindow.Closed += (sender, args) =>
                {
                    _logger?.LogWarning("MainWindow Closed event fired");
                };
                
                mainWindow.Closing += (sender, args) =>
                {
                    _logger?.LogWarning("MainWindow Closing event fired");
                };
                
                desktop.MainWindow = mainWindow;
                mainWindow.Show();
                
                _logger.LogInformation("Main window created and shown, IsVisible={IsVisible}", mainWindow.IsVisible);
            }

            base.OnFrameworkInitializationCompleted();
            
            // Perform async initialization AFTER framework initialization is complete
            // This ensures the window is shown and the message loop is running
            _ = InitializeApplicationAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogCritical(ex, "Fatal error during initialization");
            Console.Error.WriteLine($"Fatal error during initialization: {ex}");
            throw;
        }
    }
    
    private async Task InitializeApplicationAsync()
    {
        try
        {
            _logger?.LogInformation("Starting async initialization...");
            
            // Initialize application (same order as main Launcher/Program.cs)
            using (var scope = Services!.CreateScope())
            {
                var connectionClient = scope.ServiceProvider.GetRequiredService<IConnectionClient>();
                var settingsProvider = scope.ServiceProvider.GetRequiredService<SettingsProvider<Settings.Settings>>();
                var databaseContext = scope.ServiceProvider.GetRequiredService<Data.DatabaseContext>();

                // Connect to server
                _logger?.LogInformation("Connecting to server...");
                await connectionClient.ConnectAsync().ConfigureAwait(false);

                if (!await connectionClient.PingAsync().ConfigureAwait(false))
                {
                    _logger?.LogWarning("Server not reachable, enabling offline mode");
                    await connectionClient.EnableOfflineModeAsync().ConfigureAwait(false);
                }

                // Set default install directory if not configured
                if (settingsProvider.CurrentValue.Games.InstallDirectories.Length == 0)
                {
                    _logger?.LogInformation("Setting default install directory");
                    settingsProvider.Update(static s => s.Games.InstallDirectories = GetOSPlatform() switch
                    {
                        var platform when platform == OSPlatform.Windows => [Path.Combine(Path.GetPathRoot(AppContext.BaseDirectory) ?? "C:", "Games")],
                        var platform when platform == OSPlatform.Linux => [Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Games")],
                        var platform when platform == OSPlatform.OSX => [Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Games")],
                        _ => throw new NotSupportedException("Unsupported OS platform")
                    });
                }

                // Run database migrations
                _logger?.LogInformation("Running database migrations...");
                await databaseContext.Database.MigrateAsync().ConfigureAwait(false);
                _logger?.LogInformation("Database migrations complete");
            }

            // Initialize the view model on the UI thread
            var mainViewModel = Services!.GetRequiredService<MainWindowViewModel>();
            _logger?.LogInformation("Initializing view model...");
            await mainViewModel.InitializeAsync().ConfigureAwait(false);
            _logger?.LogInformation("View model initialized, application ready");
        }
        catch (Exception ex)
        {
            _logger?.LogCritical(ex, "Fatal error during async initialization");
            Console.Error.WriteLine($"Fatal error during async initialization: {ex}");
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Configure logging to console and file
        var logDirectory = Path.Combine(AppPaths.GetConfigDirectory(), "Logs");
        Directory.CreateDirectory(logDirectory);
        var logFilePath = Path.Combine(logDirectory, $"avalonia-launcher-{DateTime.Now:yyyy-MM-dd}.log");
        
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddConsole();
            builder.AddSimpleConsole(options =>
            {
                options.IncludeScopes = true;
                options.TimestampFormat = "[HH:mm:ss] ";
            });
            // Add file logging via a simple provider
            builder.AddProvider(new FileLoggerProvider(logFilePath));
        });
        
        // Add HttpClient (required by SDK services)
        services.AddHttpClient();
        
        // Configure settings from file (same as main launcher's AddSettings())
        var configurationBuilder = new ConfigurationBuilder();
        var configuration = configurationBuilder.ReadFromFile<Settings.Settings>();
        var refresher = configurationBuilder.ReadFromServer<Settings.Settings>(configuration);
        configuration = configurationBuilder.Build();
        
        services.Configure<Settings.Settings>(configuration);
        services.AddSingleton(refresher);  // Register without interface, same as main launcher
        
        // Add SDK client and Launcher services
        services.AddLANCommanderClient<Settings.Settings>();
        services.AddLANCommanderLauncher();
        
        // ViewModels
        services.AddSingleton<MainWindowViewModel>();
    }
    
    private static OSPlatform GetOSPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return OSPlatform.Windows;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return OSPlatform.Linux;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return OSPlatform.OSX;
        throw new NotSupportedException("Unsupported OS platform");
    }
}

/// <summary>
/// Simple file logger provider for debugging
/// </summary>
public class FileLoggerProvider : ILoggerProvider
{
    private readonly string _filePath;
    private readonly object _lock = new();

    public FileLoggerProvider(string filePath)
    {
        _filePath = filePath;
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(_filePath, categoryName, _lock);

    public void Dispose() { }
}

public class FileLogger : ILogger
{
    private readonly string _filePath;
    private readonly string _categoryName;
    private readonly object _lock;

    public FileLogger(string filePath, string categoryName, object lockObj)
    {
        _filePath = filePath;
        _categoryName = categoryName;
        _lock = lockObj;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var message = $"[{DateTime.Now:HH:mm:ss}] [{logLevel}] [{_categoryName}] {formatter(state, exception)}";
        if (exception != null)
            message += Environment.NewLine + exception;

        lock (_lock)
        {
            try
            {
                File.AppendAllText(_filePath, message + Environment.NewLine);
            }
            catch
            {
                // Ignore file write errors
            }
        }
    }
}
