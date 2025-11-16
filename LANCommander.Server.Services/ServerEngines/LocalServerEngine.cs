using AutoMapper;
using LANCommander.SDK;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Factories;
using LANCommander.SDK.PowerShell;
using LANCommander.Server.Data.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Enums;
using LANCommander.Server.Services.Models;
using LANCommander.Server.Services.Utilities;
using LANCommander.Server.Settings.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LANCommander.Server.Services.ServerEngines;

public class LocalServerEngine(
    ILogger<LocalServerEngine> logger,
    IMapper mapper,
    IServiceProvider serviceProvider,
    ProcessExecutionContextFactory processExecutionContextFactory,
    IOptions<SDK.Models.Settings> settings) : IServerEngine
{
    public event EventHandler<ServerStatusUpdateEventArgs>? OnServerStatusUpdate;
    public event EventHandler<ServerLogEventArgs>? OnServerLog;
    
    private Dictionary<Guid, CancellationTokenSource> _running { get; set; } = new();
    private Dictionary<Guid, ServerProcessStatus> _status { get; set; } = new();
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
                _status[server.Id] = ServerProcessStatus.Stopped;
            }
        }
    }

    public bool IsManaging(Guid serverId)
    {
        return _running.ContainsKey(serverId) || _status.ContainsKey(serverId);
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
            if (await GetStatusAsync(serverId) != ServerProcessStatus.Stopped)
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

            using (var executionContext = processExecutionContextFactory.Create())
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
                    
                    _running[server.Id] = cancellationTokenSource;

                    await executionContext.ExecuteServerAsync(mapper.Map<SDK.Models.Server>(server), cancellationTokenSource);
                    
                    if (_running.ContainsKey(server.Id))
                        _running.Remove(server.Id);
                    
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

            if (_running.ContainsKey(server.Id))
            {
                await _running[server.Id].CancelAsync();

                _running.Remove(server.Id);
            }

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

                    if (settings.Value.Debug.EnableScriptDebugging)
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

    public async Task<ServerProcessStatus> GetStatusAsync(Guid serverId)
    {
        var status = ServerProcessStatus.Stopped;
        
        if (_running.ContainsKey(serverId) && _running[serverId].IsCancellationRequested)
            status = ServerProcessStatus.Stopping;
        else if (_running.ContainsKey(serverId) && !_running[serverId].IsCancellationRequested)
            status = ServerProcessStatus.Running;

        return await Task.FromResult(status);
    }
    
    private void UpdateStatus(Data.Models.Server server, ServerProcessStatus status, Exception ex = null)
    {
        if (ex != null)
        {
            _status[server.Id] = ServerProcessStatus.Error;
            OnServerStatusUpdate?.Invoke(this, new ServerStatusUpdateEventArgs(server, ServerProcessStatus.Error, ex));
        }
        else if (!_status.ContainsKey(server.Id))
        {
            _status[server.Id] = status;
            OnServerStatusUpdate?.Invoke(this, new ServerStatusUpdateEventArgs(server, status));
        }
        else if (_status[server.Id] != status)
        {
            _status[server.Id] = status;
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