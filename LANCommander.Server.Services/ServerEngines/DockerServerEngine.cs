using AutoMapper;
using Docker.DotNet;
using Docker.DotNet.Models;
using LANCommander.SDK;
using LANCommander.SDK.Enums;
using LANCommander.SDK.PowerShell;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Enums;
using LANCommander.Server.Services.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services.ServerEngines;

public class DockerServerEngine(
    ILogger<DockerServerEngine> logger,
    IServiceProvider serviceProvider,
    IMapper mapper,
    SDK.Client client) : IServerEngine
{
    private DockerClient _docker { get; set; }
    private Dictionary<Guid, string> _tracked { get; set; } = new();
    
    public event EventHandler<ServerStatusUpdateEventArgs>? OnServerStatusUpdate;
    public event EventHandler<ServerLogEventArgs>? OnServerLog;

    public async Task Init(DockerHostConfiguration configuration)
    {
        _docker = new DockerClientConfiguration(new Uri(configuration.Address)).CreateClient();
    }

    public async Task<IEnumerable<ContainerListResponse>> GetContainersAsync()
    {
        var response = await _docker.Containers.ListContainersAsync(new ContainersListParameters()
        {
            All = true
        });

        return response;
    }
    
    public async Task StartAsync(Guid serverId)
    {
        Data.Models.Server server;

        using (var scope = serviceProvider.CreateScope())
        {
            var serverService = scope.ServiceProvider.GetRequiredService<ServerService>();
            
            server = await serverService
                .Query(q =>
                {
                    return q
                        .Include(s => s.Scripts)
                        .Include(s => s.Game)
                        .Include(s => s.ServerConsoles);
                }).GetAsync(serverId);
            
            logger?.LogInformation("Starting server container \"{ServerName}\" for game {GameName}", server.Name, server.Game?.Title);
        }
        
        foreach (var serverScript in server.Scripts.Where(s => s.Type == ScriptType.BeforeStart))
        {
            try
            {
                var script = new PowerShellScript(SDK.Enums.ScriptType.BeforeStart);

                script.AddVariable("Server", mapper.Map<SDK.Models.Server>(server));

                script.UseWorkingDirectory(server.WorkingDirectory);
                script.UseInline(serverScript.Contents);
                script.UseShellExecute();

                logger?.LogInformation("Executing script \"{ScriptName}\"", serverScript.Name);

                if (client.Scripts.Debug)
                    script.EnableDebug();

                await script.ExecuteAsync<int>();
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error running script \"{ScriptName}\" for server \"{ServerName}\"", serverScript.Name, server.Name);
            }
        }

        try
        {
            await _docker.Containers.StartContainerAsync(server.ContainerId, new ContainerStartParameters());
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Could not start server container {ServerName} ({ServerId})", server.Name, server.Id);
        }
    }

    public async Task StopAsync(Guid serverId)
    {
        using (var scope = serviceProvider.CreateScope())
        {
            var serverService = scope.ServiceProvider.GetRequiredService<ServerService>();

            var server = await serverService.GetAsync(serverId);

            logger?.LogInformation("Stopping server container \"{ServerName}\" for game {GameName}", server.Name, server.Game?.Title);

            await _docker.Containers.StopContainerAsync(server.ContainerId, new ContainerStopParameters());
            
            foreach (var serverScript in server.Scripts.Where(s => s.Type == ScriptType.AfterStop))
            {
                try
                {
                    var script = new PowerShellScript(SDK.Enums.ScriptType.AfterStop);

                    script.AddVariable("Server", mapper.Map<SDK.Models.Server>(server));

                    script.UseWorkingDirectory(server.WorkingDirectory);
                    script.UseInline(serverScript.Contents);
                    script.UseShellExecute();

                    logger?.LogInformation("Executing script \"{ScriptName}\"", serverScript.Name);

                    if (client.Scripts.Debug)
                        script.EnableDebug();

                    await script.ExecuteAsync<int>();
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Error running script \"{ScriptName}\" for server \"{ServerName}\"", serverScript.Name, server.Name);
                }
            }
        }
    }

    public async Task<ServerProcessStatus> GetStatusAsync(Guid serverId)
    {
        Data.Models.Server server;
        
        using (var scope = serviceProvider.CreateScope())
        {
            if (!_tracked.ContainsKey(serverId))
            {
                var serverService = scope.ServiceProvider.GetRequiredService<ServerService>();

                server = await serverService.GetAsync(serverId);
                
                if (server == null)
                    return ServerProcessStatus.Stopped;

                if (String.IsNullOrWhiteSpace(server.ContainerId))
                    return ServerProcessStatus.Stopped;

                _tracked[serverId] = server.ContainerId;
            }

            var container = await _docker.Containers.InspectContainerAsync(_tracked[serverId]);
            
            if (container.State.Running)
                return ServerProcessStatus.Running;

            if (container.State.Paused)
                return ServerProcessStatus.Stopped;
            
            if (container.State.Dead)
                return ServerProcessStatus.Stopped;
            
            if (container.State.Restarting)
                return ServerProcessStatus.Starting;

            return ServerProcessStatus.Stopped;
        }
    }
}