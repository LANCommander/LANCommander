using LANCommander.Launcher.Data;
using LANCommander.Launcher.Services;
using LANCommander.Launcher.Services.Extensions;
using LANCommander.Launcher.Settings;
using LANCommander.Launcher.Startup;
using LANCommander.Launcher.UI;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Photino.Blazor;
using Photino.Blazor.CustomWindow.Extensions;
using Serilog;
using Serilog.Events;
using System.Runtime.InteropServices;

// Map the Microsoft.Extensions.Logging.LogLevel to Serilog.LogEventLevel.
var serilogLogLevel = MapLogLevel(LogLevel.Debug);

using var Logger = new LoggerConfiguration()
    .MinimumLevel.Is(serilogLogLevel)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.Components", LogEventLevel.Warning)
    .MinimumLevel.Override("AntDesign", LogEventLevel.Warning)
    .Enrich.WithProperty("Application", typeof(Program).Assembly.GetName().Name)
    //.WriteTo.File(Path.Combine(settings.Debug.LoggingPath, "log-.txt"), rollingInterval: settings.Debug.LoggingArchivePeriod)
#if DEBUG
    .WriteTo.Seq("http://localhost:5341")
#endif
    .CreateLogger();

Logger.Information("Starting launcher | Version: {Version}", UpdateService.GetCurrentVersion());

var builder = PhotinoBlazorAppBuilder.CreateDefault(args);

// Configure services
builder.AddSettings();
builder.AddLogging();
builder.Services.AddCustomWindow();
builder.Services.AddAntDesign();
builder.Services.AddSingleton<LocalizationService>();
builder.Services.AddLANCommanderClient<Settings>();
builder.Services.AddLANCommanderLauncher();

// Configure root component
builder.RootComponents.Add<App_Main>("app");

var app = builder.Build();

// Configure main window
app.RegisterMainWindow()
   .RegisterMediaHandler()
   .RegisterNotificationHandler()
   .RegisterImportHandler()
   .RestoreWindowPosition();

// Initialize application
using var scope = app.Services.CreateScope();

var connectionClient = scope.ServiceProvider.GetRequiredService<IConnectionClient>();
var settingsProvider = scope.ServiceProvider.GetRequiredService<SettingsProvider<Settings>>();
var databaseContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

await connectionClient.ConnectAsync();

if (!await connectionClient.PingAsync())
    await connectionClient.EnableOfflineModeAsync();

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

await databaseContext.Database.MigrateAsync();

app.Run();

/// <summary>
/// Maps Microsoft.Extensions.Logging.LogLevel to Serilog.Events.LogEventLevel.
/// </summary>
static LogEventLevel MapLogLevel(LogLevel level)
{
    return level switch
    {
        LogLevel.Trace => LogEventLevel.Verbose,
        LogLevel.Debug => LogEventLevel.Debug,
        LogLevel.Information => LogEventLevel.Information,
        LogLevel.Warning => LogEventLevel.Warning,
        LogLevel.Error => LogEventLevel.Error,
        LogLevel.Critical => LogEventLevel.Fatal,
        // LogLevel.None indicates logging should be disabled.
        // Serilog does not have a direct "Off" level so you might choose to
        // either bypass logging configuration or set it high enough to ignore messages.
        LogLevel.None => LogEventLevel.Fatal,
        _ => LogEventLevel.Information
    };
}

static OSPlatform GetOSPlatform()
{
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        return OSPlatform.Windows;
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        return OSPlatform.Linux;
    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        return OSPlatform.OSX;
    throw new NotSupportedException("Unsupported OS platform");
}