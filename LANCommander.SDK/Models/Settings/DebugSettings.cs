using Microsoft.Extensions.Logging;

namespace LANCommander.SDK.Models;

public enum LoggingArchivePeriod
{
    Day,
    Week,
    Month,
    Year,
    Never
}

public class DebugSettings
{
    public bool EnableScriptDebugging { get; set; } = false;
    public LogLevel LogLevel { get; set; } = LogLevel.Warning;
    public string LoggingPath { get; set; } = "Logs";
    public LoggingArchivePeriod LoggingArchivePeriod { get; set; } = LoggingArchivePeriod.Day;
    public int MaxArchiveFiles { get; set; } = 10;
}