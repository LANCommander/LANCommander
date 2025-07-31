using System.Diagnostics;
using System.Text.Json.Serialization;
using LANCommander.Server.Services.Enums;

namespace LANCommander.Server.Services.Abstractions;

public interface IServerState
{
    public ServerProcessStatus Status { get; set; }
    public ulong MemoryUsage { get; set; }
    public ulong TotalMemory { get; set; }
    public double ProcessorLoad { get; set; }
    [JsonIgnore]
    public CancellationTokenSource CancellationToken { get; set; }
}