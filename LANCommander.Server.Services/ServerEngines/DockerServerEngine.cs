using AutoMapper;
using Docker.DotNet;
using Docker.DotNet.Models;
using LANCommander.SDK;
using LANCommander.SDK.Enums;
using LANCommander.SDK.PowerShell;
using LANCommander.Server.Data.Enums;
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
    private ServerEngineConfiguration _config;
    private Dictionary<Guid, DockerClient> _dockerClients = new();
    private Dictionary<Guid, DockerContainer> _tracked { get; set; } = new();
    public event EventHandler<ServerStatusUpdateEventArgs>? OnServerStatusUpdate;
    public event EventHandler<ServerLogEventArgs>? OnServerLog;

    public async Task InitializeAsync()
    {
        var settings = SettingService.GetSettings();

        foreach (var serverEngineConfig in settings.Servers.ServerEngines.Where(se => se.Type == ServerEngine.Docker))
        {
            if (serverEngineConfig != null && !String.IsNullOrWhiteSpace(serverEngineConfig.Address) && Uri.TryCreate(serverEngineConfig.Address, UriKind.Absolute, out var hostAddress))
                _dockerClients[serverEngineConfig.Id] = new DockerClientConfiguration(hostAddress).CreateClient();
        }
        
        using (var scope = serviceProvider.CreateScope())
        {
            var serverService = scope.ServiceProvider.GetRequiredService<ServerService>();

            var servers = await serverService.GetAsync(s =>
                s.Engine == ServerEngine.Docker);

            foreach (var server in servers)
            {
                if (server.DockerHostId.HasValue && _dockerClients.ContainsKey(server.DockerHostId.Value))
                    _tracked[server.Id] = new DockerContainer
                    {
                        HostId = server.DockerHostId.Value,
                        Id = server.ContainerId,
                        Name = server.Name,
                    };
            }
        }
    }

    public async Task<IEnumerable<DockerContainer>> GetContainersAsync(Guid dockerHostId)
    {
        if (!_dockerClients.ContainsKey(dockerHostId))
            return Enumerable.Empty<DockerContainer>();
        
        var response = await _dockerClients[dockerHostId].Containers.ListContainersAsync(new ContainersListParameters()
        {
            All = true
        });

        return response.Where(i => i != null).Select(i => new DockerContainer
        {
            Id = i.ID,
            HostId = dockerHostId,
            Name = i.Names.FirstOrDefault()
        });
    }

    public bool IsManaging(Guid serverId)
    {
        return _tracked.ContainsKey(serverId);
    }

    public async Task StartAsync(Guid serverId)
    {
        Data.Models.Server server;

        if (!_tracked.ContainsKey(serverId))
            throw new Exception("Server is not being tracked by this engine.");

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
                var script = new PowerShellScript(SDK.Enums.ScriptType.BeforeStart, client.Scripts);

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
            if (server.DockerHostId.HasValue)
                await _dockerClients[server.DockerHostId.Value].Containers
                    .StartContainerAsync(server.ContainerId, new ContainerStartParameters());
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Could not start server container {ServerName} ({ServerId})", server.Name, server.Id);
        }
    }

    public async Task StopAsync(Guid serverId)
    {
        if (!_tracked.ContainsKey(serverId))
            throw new Exception("Server is not being tracked by this engine.");
        
        using (var scope = serviceProvider.CreateScope())
        {
            var serverService = scope.ServiceProvider.GetRequiredService<ServerService>();

            var server = await serverService.GetAsync(serverId);

            logger?.LogInformation("Stopping server container \"{ServerName}\" for game {GameName}", server.Name, server.Game?.Title);

            if (server.DockerHostId.HasValue)
                await _dockerClients[server.DockerHostId.Value].Containers.StopContainerAsync(server.ContainerId, new ContainerStopParameters());
            
            foreach (var serverScript in server.Scripts.Where(s => s.Type == ScriptType.AfterStop))
            {
                try
                {
                    var script = new PowerShellScript(SDK.Enums.ScriptType.AfterStop, client.Scripts);

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
        Data.Models.Server server = null;
        
        using (var scope = serviceProvider.CreateScope())
        {
            if (!_tracked.ContainsKey(serverId))
            {
                var serverService = scope.ServiceProvider.GetRequiredService<ServerService>();

                server = await serverService.GetAsync(serverId);
                
                if (server == null)
                    return ServerProcessStatus.Stopped;

                if (!server.DockerHostId.HasValue)
                    return ServerProcessStatus.Stopped;

                if (String.IsNullOrWhiteSpace(server.ContainerId))
                    return ServerProcessStatus.Stopped;

                _tracked[serverId] = new DockerContainer
                {
                    Id = server.ContainerId,
                    HostId = server.DockerHostId.Value,
                    Name = server.Name,
                };
            }

            try
            {
                var container = await _dockerClients[_tracked[serverId].HostId].Containers.InspectContainerAsync(_tracked[serverId].Id);

                if (container.State.Running)
                    return ServerProcessStatus.Running;

                if (container.State.Paused)
                    return ServerProcessStatus.Stopped;

                if (container.State.Dead)
                    return ServerProcessStatus.Stopped;

                if (container.State.Restarting)
                    return ServerProcessStatus.Starting;
            }
            catch
            {
            }

            return ServerProcessStatus.Stopped;
        }
    }
}