using AutoMapper;
using LANCommander.SDK;
using LANCommander.SDK.Enums;
using LANCommander.SDK.PowerShell;
using LANCommander.Server.Data.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Enums;
using LANCommander.Server.Services.Models;
using LANCommander.Server.Services.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services.ServerEngines;

public class LocalServerEngine(
    ILogger<LocalServerEngine> logger,
    IMapper mapper,
    SDK.Client client,
    IServiceProvider serviceProvider) : IServerEngine
{
    public event EventHandler<ServerStatusUpdateEventArgs>? OnServerStatusUpdate;
    public event EventHandler<ServerLogEventArgs>? OnServerLog;
    private Dictionary<Guid, ServerProcessState> _state { get; set; } = new();
    private Dictionary<Guid, LogFileMonitor> _logFileMonitors { get; set; } = new();

    public async Task InitializeAsync()
    {
        using (var scope = serviceProvider.CreateScope())
        {
            var serverService = scope.ServiceProvider.GetRequiredService<ServerService>();

            var servers = await serverService.GetAsync(s =>
                s.Engine == ServerEngine.Local);

            foreach (var server in servers)
            {
                UpdateStatus(server, ServerProcessStatus.Stopped);
            }
        }
    }

    public bool IsManaging(Guid serverId)
    {
        return _state.ContainsKey(serverId);
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

            // Server not found
            if (server == null)
                return;

            // Don't start the server if it's already started
            if (!IsStopped(serverId))
                return;
            
            UpdateStatus(server, ServerProcessStatus.Starting);

            logger?.LogInformation("Starting server \"{ServerName}\" for game {GameName}", server.Name, server.Game?.Title);

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

                    await script.ExecuteAsync<int>();
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Error running script \"{ScriptName}\" for server \"{ServerName}\"", serverScript.Name, server.Name);
                }
            }

            using (var executionContext = new ProcessExecutionContext(client, logger))
            {
                try
                {
                    executionContext.AddVariable("ServerId", server.Id.ToString());
                    executionContext.AddVariable("ServerName", server.Name);
                    executionContext.AddVariable("ServerHost", server.Host);
                    executionContext.AddVariable("ServerPort", server.Port.ToString());

                    if (server.Game != null)
                    {
                        executionContext.AddVariable("GameTitle", server.Game?.Title);
                        executionContext.AddVariable("GameId", server.Game?.Id.ToString());   
                    }
                
                    foreach (var logFile in server.ServerConsoles.Where(sc => sc.Type == ServerConsoleType.LogFile))
                    {
                        StartMonitoringLog(logFile, server);
                    }
                
                    UpdateStatus(server, ServerProcessStatus.Running);
                    
                    var cancellationTokenSource = new CancellationTokenSource();

                    _state[server.Id].Process = executionContext.GetProcess();
                    _state[server.Id].CancellationToken = cancellationTokenSource;

                    await executionContext.ExecuteServerAsync(mapper.Map<SDK.Models.Server>(server), cancellationTokenSource);
                    
                    UpdateStatus(server, ServerProcessStatus.Stopped);
                }
                catch (Exception ex)
                {
                    UpdateStatus(server, ServerProcessStatus.Error, ex);

                    logger?.LogError(ex, "Could not start server {ServerName} ({ServerId})", server.Name, server.Id);
                }

            }
        }
    }

    public async Task StopAsync(Guid serverId)
    {
        using (var scope = serviceProvider.CreateScope())
        {
            var serverService = scope.ServiceProvider.GetRequiredService<ServerService>();

            var server = await serverService
                .Query(q =>
                {
                    return q
                        .Include(s => s.Scripts)
                        .Include(s => s.Game)
                        .Include(s => s.ServerConsoles);
                }).GetAsync(serverId);

            logger?.LogInformation("Stopping server \"{ServerName}\" for game {GameName}", server.Name, server.Game?.Title);

            UpdateStatus(server, ServerProcessStatus.Stopping);
            
            if (!_state[server.Id].CancellationToken.IsCancellationRequested)
                await _state[server.Id].CancellationToken.CancelAsync();

            if (_logFileMonitors.ContainsKey(server.Id))
            {
                _logFileMonitors[server.Id].Dispose();
                _logFileMonitors.Remove(server.Id);
            }

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

            UpdateStatus(server, ServerProcessStatus.Stopped);
        }
    }

    public async Task<IServerState> GetStateAsync(Guid serverId)
    {
        if (serverId == Guid.Empty)
            return null!;
        
        if (!_state.ContainsKey(serverId))
            _state[serverId] = new ServerProcessState();

        if (IsRunning(serverId))
        {
            _state[serverId].Status = ServerProcessStatus.Running;
            
            _state[serverId].MemoryUsage = (ulong)_state[serverId].Process.PrivateMemorySize64;
            _state[serverId].ProcessorLoad = (_state[serverId].Process.TotalProcessorTime - _state[serverId].LastMeasuredProcessorTime).TotalMilliseconds / (Environment.ProcessorCount * _state[serverId].ProcessTimer.ElapsedMilliseconds);

            _state[serverId].ProcessTimer.Restart();
            _state[serverId].LastMeasuredProcessorTime = _state[serverId].Process.TotalProcessorTime;
        }
        else if (IsStopped(serverId))
            _state[serverId].Status = ServerProcessStatus.Stopped;
        else if (IsStopping(serverId))
            _state[serverId].Status = ServerProcessStatus.Stopping;
        
        return await Task.FromResult(_state[serverId]);
    }
    
    private bool IsRunning(Guid serverId)
    {
        return !_state[serverId].CancellationToken.IsCancellationRequested && (!_state[serverId].Process?.HasExited ?? false);
    }

    private bool IsStopped(Guid serverId)
    {
        return _state[serverId].CancellationToken == null || _state[serverId].Process == null || _state[serverId].Process.HasExited;
    }

    private bool IsStopping(Guid serverId)
    {
        return _state[serverId].CancellationToken.IsCancellationRequested &&
               (_state[serverId].Process == null || _state[serverId].Process.HasExited);
    }
    

    private void UpdateStatus(Data.Models.Server server, ServerProcessStatus status, Exception ex = null)
    {
        if (!_state.ContainsKey(server.Id))
            _state[server.Id] = new ServerProcessState();
        
        if (ex != null)
        {
            _state[server.Id].Status = ServerProcessStatus.Error;
            OnServerStatusUpdate?.Invoke(this, new ServerStatusUpdateEventArgs(server, ServerProcessStatus.Error, ex));
        }
        else if (_state[server.Id].Status != status)
        {
            _state[server.Id].Status = status;
            OnServerStatusUpdate?.Invoke(this, new ServerStatusUpdateEventArgs(server, status));
        }
    }
    
    private void StartMonitoringLog(ServerConsole log, Data.Models.Server server)
    {
        if (!_logFileMonitors.ContainsKey(server.Id))
        {
            _logFileMonitors[server.Id] = new LogFileMonitor(server, log);
        }
    }
}