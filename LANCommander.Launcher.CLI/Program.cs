using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using LANCommander.Launcher.Services.Extensions;
using LANCommander.Launcher.Services;
using System.Runtime.InteropServices;
using LANCommander.Launcher.Data;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Services;
using Microsoft.EntityFrameworkCore;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Use ServiceDefaults for consistent logging, OpenTelemetry, health checks, etc.
builder.AddServiceDefaults();

builder.Services.AddLANCommanderClient<LANCommander.SDK.Models.Settings>();
builder.Services.AddLANCommanderLauncher(options =>
{

});

using IHost host = builder.Build();

host.Services.InitializeLANCommander();

using var scope = host.Services.CreateScope();
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
