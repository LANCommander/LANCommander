using System.Diagnostics;
using System.Text.Json.Serialization;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Enums;

namespace LANCommander.Server.Services.Models;

public class ServerProcessState : IServerState
{
    public ServerProcessStatus Status { get; set; } = ServerProcessStatus.Stopped;
    public ulong MemoryUsage { get; set; }
    public ulong TotalMemory { get; set; }
    public double ProcessorLoad { get; set; }
    public TimeSpan LastMeasuredProcessorTime { get; set; }
    [JsonIgnore] public Stopwatch ProcessTimer { get; set; } = new();
    [JsonIgnore]
    public Process EntryProcess { get; set; } = new();
    [JsonIgnore]
    public List<Process> Processes { get; set; } = new();
    [JsonIgnore]
    public CancellationTokenSource CancellationToken { get; set; } = new();
}