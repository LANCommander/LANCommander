#if DEBUG
using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using LANCommander.Launcher.Data;
using LANCommander.Launcher.Services;
using LANCommander.Launcher.Services.Extensions;
using LANCommander.Launcher.Services.Platform;
using LANCommander.Launcher.Plugins;
using LANCommander.Launcher.ViewModels;
using LANCommander.Launcher.Views;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notify.NET.Abstractions;

namespace LANCommander.Launcher.Fixtures;

/// <summary>
/// Everything a fixture builds against: the launcher's real service registrations, isolated from the
/// user's settings, data, network and desktop. Nothing here connects to a server or reads the local
/// library; fixtures fill view models in directly.
/// </summary>
/// <remarks>
/// Creating a context installs its services as <see cref="App.Services"/>, which views read in their
/// constructors (the view registry, plugin extensions). Disposing it puts the previous ones back.
/// </remarks>
public sealed class FixtureContext : IDisposable
{
    public const string ServerAddress = "http://lancommander.lan:1337/";

    /// <summary>What fixtures treat as the current time, for anything that says "3 days ago".</summary>
    public static readonly DateTime Now = new(2026, 9, 20, 20, 0, 0, DateTimeKind.Local);

    /// <summary>Free space every drive reports, so install dialogs read the same everywhere.</summary>
    public const long FreeSpace = 412L * 1024 * 1024 * 1024;

    /// <summary>The same background every time, so screens that show one stay comparable.</summary>
    private const string Background = "avares://LANCommander.Launcher/Assets/backgrounds/ut2004.jpg";

    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");

    private readonly ServiceProvider _services;
    private readonly IServiceProvider? _previousServices;
    private readonly string? _previousBackground;
    private readonly bool _previousScanOnStartup;
    private readonly CultureInfo _previousCulture;
    private readonly CultureInfo _previousUICulture;

    private bool _shellInitialized;

    public IServiceProvider Services => _services;

    public MainWindowViewModel Main => _services.GetRequiredService<MainWindowViewModel>();

    /// <summary>The shell with its pages built but nothing loaded. See <see cref="ShellViewModel.InitializeForFixture"/>.</summary>
    public ShellViewModel Shell
    {
        get
        {
            var shell = Main.ShellViewModel;

            if (!_shellInitialized)
            {
                shell.InitializeForFixture();
                _shellInitialized = true;
            }

            return shell;
        }
    }

    private FixtureContext()
    {
        _previousServices = App.Services;
        _previousBackground = ViewBackground.Override;
        _previousScanOnStartup = ServerSelectionViewModel.ScanOnStartup;
        _previousCulture = CultureInfo.CurrentCulture;
        _previousUICulture = CultureInfo.CurrentUICulture;

        ViewBackground.Override = Background;
        ServerSelectionViewModel.ScanOnStartup = false;
        InstallOptionsViewModel.FreeSpaceOverride = FreeSpaceFor;
        GameSaveItemViewModel.Now = () => Now;

        // Counts, sizes, dates and sort orders all follow the culture; pin it so baselines don't
        // depend on the machine that renders them.
        CultureInfo.CurrentCulture = Culture;
        CultureInfo.CurrentUICulture = Culture;

        _services = BuildServices();

        App.Services = _services;
    }

    public static FixtureContext Create() => new();

    /// <summary>The main window, title bar and all, showing <paramref name="view"/>.</summary>
    public MainWindow MainWindow(ViewModelBase view)
    {
        Main.CurrentView = view;

        return new MainWindow { DataContext = Main };
    }

    /// <summary>The main window showing the shell with <paramref name="page"/> as its content.</summary>
    public MainWindow ShellWindow(ViewModelBase page, Action<ShellViewModel>? configure = null)
    {
        var shell = Shell;

        // Set before the shell view exists so the page transition has nothing to fade from.
        shell.ContentView = page;

        configure?.Invoke(shell);

        return MainWindow(shell);
    }

    public T Get<T>() where T : notnull => _services.GetRequiredService<T>();

    public void Dispose()
    {
        App.Services = _previousServices;
        ViewBackground.Override = _previousBackground;
        ServerSelectionViewModel.ScanOnStartup = _previousScanOnStartup;
        InstallOptionsViewModel.FreeSpaceOverride = null;
        GameSaveItemViewModel.Now = () => DateTime.Now;
        CultureInfo.CurrentCulture = _previousCulture;
        CultureInfo.CurrentUICulture = _previousUICulture;

        // Some singletons (the packaging session) only dispose asynchronously. Off the UI thread, so
        // nothing they await can deadlock against it.
        Task.Run(() => _services.DisposeAsync().AsTask()).GetAwaiter().GetResult();
    }

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddHttpClient();

        services.Configure<Settings.Settings>(ConfigureSettings);
        services.AddSingleton<IServerConfigurationRefresher, FixtureConfigurationRefresher>();

        services.AddLANCommanderClient<Settings.Settings>();
        services.AddLANCommanderLauncher();

        // An empty in-memory database, so a stray query fails instead of reading the user's library.
        services.RemoveAll<DbContextOptions<DatabaseContext>>();
        services.RemoveAll<DbContextOptions>();
        services.AddDbContext<DbContext, DatabaseContext>(options => options.UseSqlite("Data Source=:memory:"));

        // Persist to a scratch file: a fixture that changes a setting must never touch the user's.
        var settingsPath = Path.Combine(Path.GetTempPath(), "LANCommander.Fixtures", Settings.Settings.SETTINGS_FILE_NAME);
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);

        services.Replace(ServiceDescriptor.Singleton(sp => new SettingsProvider<Settings.Settings>(
            sp.GetRequiredService<IOptionsMonitor<Settings.Settings>>(), settingsPath)));

        // Mirrors App.ConfigureServices, with the platform services swapped for inert ones.
        services.AddSingleton<InstallService>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<IBatteryService, NullBatteryService>();
        services.AddSingleton<IVolumeService, NullVolumeService>();
        services.AddSingleton<INotificationService, FixtureNotificationService>();
        services.AddSingleton<ITaskbarProgressService, FixtureTaskbarProgressService>();
        services.AddSingleton<NotificationService>();
        services.AddSingleton<TaskbarProgressService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IViewRegistry>(_ => App.CreateViewRegistry());

        return services.BuildServiceProvider();
    }

    private static (string Name, long AvailableFreeSpace)? FreeSpaceFor(string? directory)
    {
        // Drive letters are parsed by hand: on Linux Path.GetPathRoot has no idea what C: is.
        if (directory is not { Length: >= 2 } || directory[1] != ':')
            return null;

        return (directory[..2], FreeSpace);
    }

    private static void ConfigureSettings(Settings.Settings settings)
    {
        settings.Authentication.ServerAddress = new Uri(ServerAddress);
        settings.Games.InstallDirectories = [@"C:\Games", @"D:\Games"];
    }
}
#endif
