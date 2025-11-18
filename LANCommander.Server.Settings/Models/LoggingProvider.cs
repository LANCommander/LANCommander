using LANCommander.Server.Settings.Enums;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Settings.Models;

public class LoggingProvider
{
    public string Name { get; set; } = "Console";
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;
    public bool Enabled { get; set; } = true;
    public LoggingProviderType Type { get; set; } = LoggingProviderType.Console;
    public LogInterval? ArchiveEvery { get; set; }
    public int MaxArchiveFiles { get; set; } = 10;
    public string ConnectionString { get; set; } = String.Empty;
}