using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using LANCommander.Launcher.Input;
using LANCommander.Launcher.Helpers;
using LANCommander.Launcher.Plugins;
using LANCommander.Launcher.Services;
using LANCommander.Launcher.ViewModels;
using LANCommander.Launcher.Views;
using LANCommander.Launcher.Services;
using LANCommander.Launcher.Services.Extensions;
using LANCommander.Launcher.Services.Platform;
using LANCommander.SDK;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Providers;
using LANCommander.SDK.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Notify.NET.Extensions;

namespace LANCommander.Launcher;

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

                if (Program.BigScreenMode)
                    mainViewModel.SetBigScreenMode();

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

                // Bind the taskbar progress indicator to the main window handle. This must be
                // wired BEFORE Show(): on Windows, Show() raises Opened synchronously, so a
                // handler attached afterwards would never fire and the progress bar would stay
                // bound to Notify.NET's default GetConsoleWindow() target instead of the app.
                void BindTaskbarProgress()
                {
                    var hwnd = mainWindow.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                    if (hwnd != IntPtr.Zero)
                        Services.GetRequiredService<TaskbarProgressService>().Initialize(hwnd);
                }

                mainWindow.Opened += (_, _) => BindTaskbarProgress();

                mainWindow.Show();

                // If Opened already fired synchronously during Show(), the handler above missed
                // it; bind now since the handle is available once the window is shown.
                BindTaskbarProgress();

                // System tray icon: the main window hides to tray on close, so the tray
                // provides navigation and an exit path. See TrayIconExtensions.
                var trayIcon = mainWindow.CreateTrayIcon(mainViewModel);
                TrayIcon.SetIcons(this, new TrayIcons { trayIcon });

                // Single-instance pipe server: forward notification-click navigations
                var singleInstance = Services.GetRequiredService<SingleInstanceService>();
                singleInstance.RegisterProtocolHandler();
                singleInstance.StartServer();
                singleInstance.NavigateToGameRequested += async (_, gameId) =>
                {
                    // The window may be hidden in the tray; surface it on the UI thread
                    // before navigating (this fires from the named-pipe listener thread).
                    Avalonia.Threading.Dispatcher.UIThread.Post(mainWindow.RestoreFromTray);
                    var shell = Services.GetRequiredService<MainWindowViewModel>().ShellViewModel;
                    await shell.NavigateToGameByIdAsync(gameId).ConfigureAwait(false);
                };
                // A second launch (e.g. the user forgot it was hiding in the tray) asks the
                // running instance to restore its window instead of opening a duplicate.
                singleInstance.RestoreRequested += (_, _) =>
                    Avalonia.Threading.Dispatcher.UIThread.Post(mainWindow.RestoreFromTray);
                mainWindow.Closed += (_, _) => singleInstance.Dispose();

                // Start gamepad navigation (gracefully disabled if SDL3 is absent)
                var gamepadService = Services.GetRequiredService<GamepadService>();
                mainWindow.Closed += (_, _) => gamepadService.Stop();
                gamepadService.Start();

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
                await databaseContext.EnableWalModeAsync().ConfigureAwait(false);
                _logger?.LogInformation("Database migrations complete");
            }

            // Initialize the view model on the UI thread
            var mainViewModel = Services!.GetRequiredService<MainWindowViewModel>();
            _logger?.LogInformation("Initializing view model...");
            await mainViewModel.InitializeAsync().ConfigureAwait(false);
            _logger?.LogInformation("View model initialized, application ready");

            // Initialize plugins now that the service provider and core services are ready.
            await Services!.GetRequiredService<LANCommander.SDK.Plugins.PluginLoaderService>()
                .InitializeAllAsync(Services!).ConfigureAwait(false);

            // Register plugin navigable views with the shared registry so the shell's
            // content control can render them. The registry's data template reads its registration
            // list live, so mappings added here are picked up even though the template was attached
            // when the shell view was constructed.
            RegisterPluginNavigationViews();
        }
        catch (Exception ex)
        {
            _logger?.LogCritical(ex, "Fatal error during async initialization");
            Console.Error.WriteLine($"Fatal error during async initialization: {ex}");
        }
    }

    private static void RegisterPluginNavigationViews()
    {
        if (Services is null)
            return;

        var registry = Services.GetService<IViewRegistry>();

        if (registry is null)
            return;

        foreach (var extension in Services.GetServices<Plugins.Extensions.INavigationPageExtension>())
        {
            try
            {
                registry.Register(extension.ViewModelType, extension.BuildView);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Could not register navigation view for plugin extension {Extension}", extension.GetType().FullName);
            }
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
            builder.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
            builder.AddFilter("System.Net.Http", LogLevel.Warning);
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

        // InstallService must be a singleton so all consumers (GameActionBarViewModel,
        // DownloadQueueViewModel, etc.) share the same queue and event subscriptions.
        // This overrides the scoped registration from AddLANCommanderLauncher().
        services.AddSingleton<InstallService>();

        // ViewModels
        services.AddSingleton<MainWindowViewModel>();

        // Input
        services.AddSingleton<GamepadService>();

        // Big screen status bar (battery/volume) — platform backend selected by OS. Only invoked
        // while in big screen mode.
        if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<IBatteryService, WindowsBatteryService>();
            services.AddSingleton<IVolumeService, WindowsVolumeService>();
        }
        else if (OperatingSystem.IsLinux())
        {
            services.AddSingleton<IBatteryService, LinuxBatteryService>();
            services.AddSingleton<IVolumeService, LinuxVolumeService>();
        }
        else if (OperatingSystem.IsMacOS())
        {
            services.AddSingleton<IBatteryService, MacBatteryService>();
            services.AddSingleton<IVolumeService, MacVolumeService>();
        }
        else
        {
            services.AddSingleton<IBatteryService, NullBatteryService>();
            services.AddSingleton<IVolumeService, NullVolumeService>();
        }

        // Platform services
        services.AddNotifications(opts =>
        {
            opts.AppName = "LANCommander";
            opts.AppUserModelId = "LANCommander.Launcher";
        });
        services.AddTaskbarProgress(opts =>
        {
            opts.DesktopFileId = "LANCommander.Launcher";
        });
        services.AddSingleton<NotificationService>();
        services.AddSingleton<TaskbarProgressService>();
        services.AddSingleton<SingleInstanceService>();
        services.AddSingleton<INavigationService, NavigationService>();

        // View registry: seed the built-in view model -> view mappings that were previously declared
        // as inline DataTemplates in MainWindow.axaml / ShellView.axaml. Plugins may append further
        // mappings during initialization. Ordering preserves the DepotGameDetailViewModel-before-
        // GameDetailViewModel rule via most-derived-first matching in ViewRegistry.
        services.AddSingleton<IViewRegistry>(_ =>
        {
            var registry = new ViewRegistry();

            // App-level shell hosted in MainWindow's ContentControl
            registry.Register<SplashViewModel>(() => new SplashView());
            registry.Register<ServerSelectionViewModel>(() => new ServerSelectionView());
            registry.Register<LoginViewModel>(() => new LoginView());
            registry.Register<ShellViewModel>(() => new ShellView());

            // Shell content hosted in ShellView's TransitioningContentControl
            registry.Register<DepotViewModel>(() => new DepotView());
            registry.Register<DepotBrowseViewModel>(() => new DepotBrowseView());
            registry.Register<DepotGameDetailViewModel>(() => new GameDetailView());
            registry.Register<GamesListViewModel>(() => new GamesListView());
            registry.Register<LibraryViewModel>(() => new GamesListView());
            registry.Register<GameDetailViewModel>(() => new GameDetailView());
            registry.Register<SettingsViewModel>(() => new SettingsView());
            registry.Register<DownloadQueueViewModel>(() => new DownloadQueuePageView());

            return registry;
        });

        // Plugin framework: discover drop-in plugins and let them register services. Must be the last
        // registration step because the service provider is built immediately after this method returns.
        LANCommander.SDK.Plugins.PluginBootstrap.ConfigurePlugins(services, LANCommander.SDK.Plugins.PluginHost.Launcher);
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
