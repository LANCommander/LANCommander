using Microsoft.Extensions.Logging;
using Serilog;

namespace LANCommander.SDK.Models;

public interface IDebugSettings
{
    public bool EnableScriptDebugging { get; set; }
    public LogLevel LogLevel { get; set; }
    public string LoggingPath { get; set; }
    public RollingInterval LoggingArchivePeriod { get; set; }
    public int MaxArchiveFiles { get; set; }
}