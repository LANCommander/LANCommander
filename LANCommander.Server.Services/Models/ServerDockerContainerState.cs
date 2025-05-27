using System.Diagnostics;
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
    public DockerClient Client { get; set; }
    public DockerContainer Container { get; set; }
    public CancellationTokenSource CancellationToken { get; set; } = new();
}