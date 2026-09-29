using System.Collections.Concurrent;
using System.Diagnostics;
using LANCommander.SDK;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Factories;
using LANCommander.SDK.PowerShell;
using LANCommander.Server.Data.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Enums;
using LANCommander.Server.Services.Mappers;
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
    SdkMapper sdkMapper,
    IServiceProvider serviceProvider,
    PowerShellScriptFactory powerShellScriptFactory,
    ProcessExecutionContextFactory processExecutionContextFactory,
    IOptions<SDK.Models.Settings> settings) : IServerEngine
{
    public event EventHandler<ServerStatusUpdateEventArgs>? OnServerStatusUpdate;
    public event EventHandler<ServerLogEventArgs>? OnServerLog;

    private sealed class ServerRuntimeState
    {
        public ServerProcessStatus Status { get; set; } = ServerProcessStatus.Stopped;
        public CancellationTokenSource? Cancellation { get; set; }

        // Set while the server process is running
        public ProcessExecutionContext? ExecutionContext { get; set; }
        public Process? Process { get; set; }
        public int? Pid { get; set; }
        public DateTime? StartTime { get; set; }

        // Previous CPU sample, for computing usage over the interval between samples
        public readonly object SampleLock = new();
        public DateTime? LastSampleAt { get; set; }
        public TimeSpan LastProcessorTime { get; set; }
        public double? LastCpuPercent { get; set; }

        public void ClearProcess()
        {
            ExecutionContext = null;
            Process = null;
            Pid = null;
            StartTime = null;

            lock (SampleLock)
            {
                LastSampleAt = null;
                LastProcessorTime = TimeSpan.Zero;
                LastCpuPercent = null;
            }
        }
    }

    // Samples closer together than this reuse the previous CPU reading rather than measuring a
    // near-empty window (several watchers may ask at once).
    private static readonly TimeSpan MinimumSampleInterval = TimeSpan.FromMilliseconds(500);

    private readonly ConcurrentDictionary<Guid, ServerRuntimeState> _servers = new();
    private readonly Dictionary<Guid, LogFileMonitor> _logFileMonitors = new();
    private readonly HashSet<Guid> _tracked = new();

    // Guards status transitions and the tracked set so concurrent start/stop/refresh
    // calls (the engine is a singleton) can't race into a double-start or lost update.
    private readonly object _lock = new();

    public Task InitializeAsync()
    {
        return RefreshTrackingAsync();
    }

    public async Task RefreshTrackingAsync()
    {
        using var scope = serviceProvider.CreateScope();
        var serverService = scope.ServiceProvider.GetRequiredService<ServerService>();

        var servers = await serverService.GetAsync(s => s.Engine == ServerEngine.Local);
        var localServerIds = servers.Select(s => s.Id).ToHashSet();

        lock (_lock)
        {
            // Track every local server so freshly added/edited servers are immediately
            // manageable (and therefore autostartable) without needing a restart.
            foreach (var serverId in localServerIds)
            {
                _tracked.Add(serverId);
                _servers.GetOrAdd(serverId, _ => new ServerRuntimeState());
            }

            // Drop servers that are no longer local engine servers.
            foreach (var serverId in _tracked.Where(id => !localServerIds.Contains(id)).ToList())
                _tracked.Remove(serverId);
        }
    }

    public bool IsManaging(Guid serverId)
    {
        lock (_lock)
            return _tracked.Contains(serverId);
    }

    public async Task StartAsync(Guid serverId)
    {
        using var scope = serviceProvider.CreateScope();
        var serverService = scope.ServiceProvider.GetRequiredService<ServerService>();

        var server = await serverService
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

        CancellationTokenSource cancellationTokenSource;

        // Atomic gate: only the call that transitions a stopped server into the "claimed"
        // state (Cancellation set) is allowed to proceed. Any concurrent start — e.g. two
        // players launching the same game at once — sees the claim and bails, so the
        // process is never launched twice.
        lock (_lock)
        {
            var state = _servers.GetOrAdd(serverId, _ => new ServerRuntimeState());

            if (state.Status != ServerProcessStatus.Stopped || state.Cancellation != null)
                return;

            cancellationTokenSource = new CancellationTokenSource();
            state.Cancellation = cancellationTokenSource;
        }

        EmitStatus(server, ServerProcessStatus.Starting);

        logger?.LogInformation("Starting server \"{ServerName}\" for game {GameName}", server.Name, server.Game?.Title);

        foreach (var serverScript in server.Scripts.Where(s => s.Type == ScriptType.BeforeStart))
        {
            try
            {
                var script = powerShellScriptFactory.Create(ScriptType.BeforeStart);

                script.AddVariable("Server", sdkMapper.ToSdk(server));

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

                executionContext.OutputDataReceived += (_, e) => EmitLog(serverId, e.Data);
                executionContext.ErrorDataReceived += (_, e) => EmitLog(serverId, e.Data);
                executionContext.ProcessStarted += (_, process) => TrackProcess(serverId, executionContext, process);

                EmitStatus(server, ServerProcessStatus.Running);

                await executionContext.ExecuteServerAsync(sdkMapper.ToSdk(server), cancellationTokenSource);

                EmitStatus(server, ServerProcessStatus.Stopped);
            }
            catch (Exception ex)
            {
                EmitStatus(server, ServerProcessStatus.Error, ex);

                logger?.LogError(ex, "Could not start server {ServerName} ({ServerId})", server.Name, server.Id);
            }
            finally
            {
                lock (_lock)
                {
                    if (_servers.TryGetValue(serverId, out var state))
                    {
                        state.Cancellation = null;
                        state.ClearProcess();
                    }
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

            if (server == null)
                return;

            logger?.LogInformation("Stopping server \"{ServerName}\" for game {GameName}", server.Name, server.Game?.Title);

            EmitStatus(server, ServerProcessStatus.Stopping);

            CancellationTokenSource cancellationTokenSource;

            lock (_lock)
            {
                _servers.TryGetValue(server.Id, out var state);
                cancellationTokenSource = state?.Cancellation;
            }

            if (cancellationTokenSource != null)
                await cancellationTokenSource.CancelAsync();

            if (_logFileMonitors.ContainsKey(server.Id))
            {
                _logFileMonitors[server.Id].Dispose();
                _logFileMonitors.Remove(server.Id);
            }

            foreach (var serverScript in server.Scripts.Where(s => s.Type == ScriptType.AfterStop))
            {
                try
                {
                    var script = powerShellScriptFactory.Create(ScriptType.AfterStop);

                    script.AddVariable("Server", sdkMapper.ToSdk(server));

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

            EmitStatus(server, ServerProcessStatus.Stopped);
        }
    }

    public Task<ServerProcessStatus> GetStatusAsync(Guid serverId)
    {
        var status = _servers.TryGetValue(serverId, out var state)
            ? state.Status
            : ServerProcessStatus.Stopped;

        return Task.FromResult(status);
    }

    public Task<ServerProcessInfo?> GetProcessInfoAsync(Guid serverId)
    {
        if (!_servers.TryGetValue(serverId, out var state))
            return Task.FromResult<ServerProcessInfo?>(null);

        Process? process;
        int? pid;
        DateTime? startTime;

        lock (_lock)
        {
            process = state.Process;
            pid = state.Pid;
            startTime = state.StartTime;
        }

        if (process == null)
            return Task.FromResult<ServerProcessInfo?>(null);

        try
        {
            process.Refresh();

            if (process.HasExited)
                return Task.FromResult<ServerProcessInfo?>(null);

            var now = DateTime.UtcNow;
            var processorTime = process.TotalProcessorTime;
            double? cpuPercent;

            lock (state.SampleLock)
            {
                if (state.LastSampleAt.HasValue && now - state.LastSampleAt.Value < MinimumSampleInterval)
                {
                    cpuPercent = state.LastCpuPercent;
                }
                else
                {
                    // The first sample averages over the process's whole lifetime
                    var windowStart = state.LastSampleAt ?? startTime ?? now;
                    var previousProcessorTime = state.LastSampleAt.HasValue ? state.LastProcessorTime : TimeSpan.Zero;

                    cpuPercent = ProcessCpuSampler.ComputeCpuPercent(
                        processorTime - previousProcessorTime,
                        now - windowStart,
                        Environment.ProcessorCount);

                    state.LastSampleAt = now;
                    state.LastProcessorTime = processorTime;
                    state.LastCpuPercent = cpuPercent;
                }
            }

            return Task.FromResult<ServerProcessInfo?>(new ServerProcessInfo(pid, startTime, cpuPercent, process.WorkingSet64));
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            // The process exited or was disposed between the checks above
            return Task.FromResult<ServerProcessInfo?>(null);
        }
    }

    public bool CanSendInput(Guid serverId)
    {
        ProcessExecutionContext? executionContext;

        lock (_lock)
            executionContext = _servers.TryGetValue(serverId, out var state) ? state.ExecutionContext : null;

        return executionContext?.CanWriteInput ?? false;
    }

    public async Task<bool> SendInputAsync(Guid serverId, string line)
    {
        ProcessExecutionContext? executionContext;

        lock (_lock)
            executionContext = _servers.TryGetValue(serverId, out var state) ? state.ExecutionContext : null;

        if (executionContext == null)
            return false;

        return await executionContext.WriteInputLineAsync(line);
    }

    private void TrackProcess(Guid serverId, ProcessExecutionContext executionContext, Process process)
    {
        int? pid = null;
        DateTime? startTime = null;

        try
        {
            pid = process.Id;
            startTime = process.StartTime.ToUniversalTime();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            // Exited immediately, or the start time isn't readable on this platform
            startTime ??= DateTime.UtcNow;
        }

        lock (_lock)
        {
            var state = _servers.GetOrAdd(serverId, _ => new ServerRuntimeState());

            state.ExecutionContext = executionContext;
            state.Process = process;
            state.Pid = pid;
            state.StartTime = startTime;
        }
    }

    private void EmitLog(Guid serverId, string? line)
    {
        if (line == null)
            return;

        try
        {
            OnServerLog?.Invoke(this, new ServerLogEventArgs(serverId, line));
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Could not publish output from server {ServerId}", serverId);
        }
    }

    private void EmitStatus(Data.Models.Server server, ServerProcessStatus status, Exception ex = null)
    {
        var effectiveStatus = ex != null ? ServerProcessStatus.Error : status;
        bool changed;

        lock (_lock)
        {
            var state = _servers.GetOrAdd(server.Id, _ => new ServerRuntimeState());

            // Always surface errors; otherwise only fire on an actual transition so we
            // don't spam SignalR subscribers with duplicate statuses.
            changed = ex != null || state.Status != effectiveStatus;
            state.Status = effectiveStatus;
        }

        if (changed)
            OnServerStatusUpdate?.Invoke(this, new ServerStatusUpdateEventArgs(server, effectiveStatus, ex));
    }
    
    private void StartMonitoringLog(ServerConsole log, Data.Models.Server server)
    {
        if (!_logFileMonitors.ContainsKey(server.Id))
        {
            _logFileMonitors[server.Id] = new LogFileMonitor(server, log);
        }
    }
}
