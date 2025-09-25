using Microsoft.Extensions.Logging;
using Serilog;

namespace LANCommander.SDK.Models;

public class DebugSettings : IDebugSettings
{
    public bool EnableScriptDebugging { get; set; } = false;
    public LogLevel LogLevel { get; set; } = LogLevel.Warning;
    public string LoggingPath { get; set; } = "Logs";
    public RollingInterval LoggingArchivePeriod { get; set; } = RollingInterval.Day;
    public int MaxArchiveFiles { get; set; } = 10;
}