using System.Diagnostics;
using LANCommander.Server.Services.Enums;

namespace LANCommander.Server.Services.Models;

public class ServerProcessState
{
    public ServerProcessStatus Status { get; set; } = ServerProcessStatus.Stopped;
    public long MemoryUsage { get; set; }
    public double ProcessorLoad { get; set; }
    public TimeSpan LastMeasuredProcessorTime { get; set; }
    public Stopwatch ProcessTimer { get; set; } = new();
    public Process Process { get; set; }
    public CancellationTokenSource CancellationToken { get; set; } = new();
}