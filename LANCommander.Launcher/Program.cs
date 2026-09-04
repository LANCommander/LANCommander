using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using LANCommander.Launcher.Services;
using LANCommander.Launcher.ViewModels;
using LANCommander.Launcher.Views;
using LANCommander.Launcher.Data;
using LANCommander.Launcher.Services;
using LANCommander.Launcher.Services.Extensions;
using LANCommander.SDK;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher;

class Program
{
    // Used to pass the service provider and args into ScriptDebugApp
    internal static IServiceProvider? HeadlessServiceProvider;
    internal static string[]? HeadlessArgs;
    internal static bool BigScreenMode;

    private static readonly string[] CliVerbs =
    [
        "RunScript", "Install", "Uninstall", "Run", "Sync",
        "Import", "Export", "Upload", "Login", "Logout", "ChangeAlias"
    ];

    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Any(a => CliVerbs.Any(v => v.Equals(a, StringComparison.OrdinalIgnoreCase)))
            || args.Any(a => a is "--help" or "--version"))
        {
            RunHeadlessAsync(args).GetAwaiter().GetResult();
            return;
        }

        // If a protocol arg like "lancommander://game/{guid}" is present, this
        // is a secondary instance spawned by a notification click.  Forward the
        // navigation request to the already-running instance, then exit.
        foreach (var arg in args)
        {
            var gameId = SingleInstanceService.ParseProtocolArg(arg);
            if (gameId.HasValue)
            {
                SingleInstanceService.TrySendToServer($"navigate-game:{gameId}");
                return;
            }
        }

        // Only one GUI instance should run at a time. The launcher hides to the tray on
        // close, so a user may relaunch it without realizing it's still running. If another
        // instance already holds the lock, ask it to surface its window, then exit.
        if (!SingleInstanceService.TryAcquireInstanceLock())
        {
            SingleInstanceService.TrySendToServer("restore");
            return;
        }

        BigScreenMode = args.Any(a => a.Equals("--big-screen", StringComparison.OrdinalIgnoreCase));

        BuildAvaloniaApp()
#if DEBUG
            .WithDeveloperTools()
#endif
            .StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    static async Task RunHeadlessAsync(string[] args)
    {
        ApplyDataDirectoryOverride(args);

        var configurationBuilder = new ConfigurationBuilder();
        var fileConfiguration = configurationBuilder.ReadFromFile<Settings.Settings>();
        var refresher = configurationBuilder.ReadFromServer<Settings.Settings>(fileConfiguration);

        IConfiguration configuration = configurationBuilder.Build();

        var settings = new Settings.Settings();
        configuration.Bind(settings);

        var services = new ServiceCollection();

        var logDirectory = Path.Combine(AppPaths.GetConfigDirectory(), "Logs");

        Directory.CreateDirectory(logDirectory);

        var logFilePath = Path.Combine(logDirectory, $"avalonia-launcher-{DateTime.Now:yyyy-MM-dd}.log");

        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddProvider(new FileLoggerProvider(logFilePath));
            builder.SetMinimumLevel(LogLevel.Information);
        });

        services.Configure<Settings.Settings>(configuration);
        services.AddSingleton(refresher);

        services.AddHttpClient();

        services.AddLANCommanderClient<Settings.Settings>();
        services.AddLANCommanderLauncher();

        services.AddSingleton<InstallService>();
        services.AddSingleton<SingleInstanceService>();

        var serviceProvider = services.BuildServiceProvider();

        if (settings.Debug.EnableScriptDebugging)
        {
            HeadlessServiceProvider = serviceProvider;
            HeadlessArgs = args;

            AppBuilder.Configure<ScriptDebugApp>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace()
                .StartWithClassicDesktopLifetime(args);
        }
        else
        {
            using var scope = serviceProvider.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            try
            {
                var connectionClient = scope.ServiceProvider.GetRequiredService<IConnectionClient>();
                var commandLineService = scope.ServiceProvider.GetRequiredService<CommandLineService>();
                var settingsProvider = scope.ServiceProvider.GetRequiredService<SettingsProvider<Settings.Settings>>();
                var databaseContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

                logger.LogInformation("Running headless with data directory {DataDirectory}", AppPaths.GetConfigDirectory());

                await connectionClient.ConnectAsync().ConfigureAwait(false);

                if (!await connectionClient.PingAsync().ConfigureAwait(false))
                    await connectionClient.EnableOfflineModeAsync().ConfigureAwait(false);

                if (settingsProvider.CurrentValue.Games.InstallDirectories.Length == 0)
                {
                    settingsProvider.Update(static s => s.Games.InstallDirectories = GetOSPlatform() switch
                    {
                        var platform when platform == OSPlatform.Windows => [Path.Combine(Path.GetPathRoot(AppContext.BaseDirectory) ?? "C:", "Games")],
                        var platform when platform == OSPlatform.Linux => [Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Games")],
                        var platform when platform == OSPlatform.OSX => [Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Games")],
                        _ => throw new NotSupportedException("Unsupported OS platform")
                    });
                }

                await databaseContext.Database.MigrateAsync().ConfigureAwait(false);
                await databaseContext.EnableWalModeAsync().ConfigureAwait(false);

                await commandLineService.ParseCommandLineAsync(args);
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Headless run failed before completing");

                Environment.ExitCode = 1;
            }
        }
    }

    private static void ApplyDataDirectoryOverride(string[] args)
    {
        var index = Array.FindIndex(args, a => a.Equals("--DataDirectory", StringComparison.OrdinalIgnoreCase));

        if (index < 0 || index + 1 >= args.Length)
            return;

        var dataDirectory = args[index + 1];

        if (string.IsNullOrWhiteSpace(dataDirectory))
            return;

        try
        {
            AppPaths.UseConfigDirectory(dataDirectory);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Could not use data directory '{dataDirectory}': {ex.Message}");
        }
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
/// Minimal Avalonia application used when EnableScriptDebugging is true.
/// Shows only a PowerShellConsoleWindow and runs the requested script inside it.
/// </summary>
internal class ScriptDebugApp : Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

            var vm = new PowerShellConsoleViewModel("Script Debugger", string.Empty);
            var window = new PowerShellConsoleWindow { DataContext = vm };
            vm.CloseAction = () => window.Close();

            desktop.MainWindow = window;
            window.Show();

            _ = RunScriptAsync(window, Program.HeadlessServiceProvider!, Program.HeadlessArgs!);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task RunScriptAsync(
        PowerShellConsoleWindow window,
        IServiceProvider serviceProvider,
        string[] args)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var connectionClient = scope.ServiceProvider.GetRequiredService<IConnectionClient>();
            var commandLineService = scope.ServiceProvider.GetRequiredService<CommandLineService>();
            var settingsProvider = scope.ServiceProvider.GetRequiredService<SettingsProvider<Settings.Settings>>();
            var databaseContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

            await connectionClient.ConnectAsync().ConfigureAwait(false);

            if (!await connectionClient.PingAsync().ConfigureAwait(false))
                await connectionClient.EnableOfflineModeAsync().ConfigureAwait(false);

            if (settingsProvider.CurrentValue.Games.InstallDirectories.Length == 0)
            {
                settingsProvider.Update(static s => s.Games.InstallDirectories = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? [Path.Combine(Path.GetPathRoot(AppContext.BaseDirectory) ?? "C:", "Games")]
                    : [Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Games")]);
            }

            await databaseContext.Database.MigrateAsync().ConfigureAwait(false);
            await databaseContext.EnableWalModeAsync().ConfigureAwait(false);

            await commandLineService.ParseCommandLineAsync(args).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            window.ConsoleControl.OnOutput(LogLevel.Error, $"Fatal error: {ex.Message}");
        }
    }
}
