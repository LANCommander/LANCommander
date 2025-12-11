using LANCommander.SDK;
using LANCommander.Server.Settings.Enums;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Settings.Models;

public class LogSettings
{
    public bool IgnorePings { get; set; } = true;
    public IEnumerable<LoggingProvider> Providers { get; set; } = 
    [
        new()
        {
            Name = "Console",
            MinimumLevel = LogLevel.Information, Type = LoggingProviderType.Console
        },
        new()
        {
            Name = "File",
            MinimumLevel = LogLevel.Information,
            Type = LoggingProviderType.File,
            ConnectionString = AppPaths.GetConfigPath("Logs")
        },
        new()
        {
            Name = "Server Console",
            MinimumLevel = LogLevel.Information,
            Type = LoggingProviderType.SignalR
        },
    ];
}