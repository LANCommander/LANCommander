using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using LANCommander.Launcher.Services.Extensions;
using LANCommander.Launcher.Services;
using System.Runtime.InteropServices;
using LANCommander.Launcher.Data;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Extensions.Logging;
using Serilog.Events;
using YamlDotNet.Serialization;

// Map the Microsoft.Extensions.Logging.LogLevel to Serilog.LogEventLevel.

using var logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.Components", LogEventLevel.Warning)
    .MinimumLevel.Override("AntDesign", LogEventLevel.Warning)
    .Enrich.WithProperty("Application", typeof(Program).Assembly.GetName().Name)
    .WriteTo.Console()
#if DEBUG
    .WriteTo.Seq("http://localhost:5341")
#endif
    .CreateLogger();

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.ClearProviders();
    loggingBuilder.AddSerilog(logger);
});

builder.Services.AddLANCommanderClient<LANCommander.SDK.Models.Settings>();
builder.Services.AddLANCommanderLauncher(options =>
{

});

using IHost host = builder.Build();

host.Services.InitializeLANCommander();

using (var scope = host.Services.CreateScope())
{
    var connectionClient = scope.ServiceProvider.GetRequiredService<IConnectionClient>();
    var settingsProvider = scope.ServiceProvider.GetRequiredService<SettingsProvider<LANCommander.Launcher.Settings.Settings>>();
    var databaseContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
    var commandLineService = scope.ServiceProvider.GetRequiredService<CommandLineService>();

    if (!await connectionClient.PingAsync())
        await connectionClient.EnableOfflineModeAsync();

    if (settingsProvider.CurrentValue.Games.InstallDirectories.Length == 0)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            settingsProvider.Update(s =>
            {
                s.Games.InstallDirectories = [Path.Combine(Path.GetPathRoot(AppContext.BaseDirectory) ?? "C:", "Games")];
            });
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            settingsProvider.Update(s =>
            {
                s.Games.InstallDirectories = [Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Games")];
            });
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            settingsProvider.Update(s =>
            {
                s.Games.InstallDirectories = [Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Games")];
            });
    }

    await databaseContext.Database.MigrateAsync();
    
    await commandLineService.ParseCommandLineAsync(args);
}


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