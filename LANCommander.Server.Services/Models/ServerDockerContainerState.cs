using System.Diagnostics;
using System.Text.Json.Serialization;
using Docker.DotNet;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Enums;

namespace LANCommander.Server.Services.Models;

public class ServerDockerContainerState : IServerState
{
    public ServerProcessStatus Status { get; set; } = ServerProcessStatus.Stopped;
    public ulong MemoryUsage { get; set; }
    public ulong TotalMemory { get; set; }
    public double ProcessorLoad { get; set; }
    [JsonIgnore]
    public DockerClient Client { get; set; }
    [JsonIgnore]
    public DockerContainer Container { get; set; }
    [JsonIgnore]
    public CancellationTokenSource CancellationToken { get; set; } = new();
}