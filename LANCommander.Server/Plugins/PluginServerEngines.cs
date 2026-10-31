using System.Collections.Concurrent;
using LANCommander.SDK.PowerShell;
using LANCommander.SDK.Enums;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Enums;
using LANCommander.Server.Services.Mappers;
using LANCommander.Server.Services.Models;
using LANCommander.Server.Services.Utilities;
using LANCommander.Server.Settings.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ServerEntity = LANCommander.Server.Data.Models.Server;

namespace LANCommander.Server.Plugins;

/// <summary>
/// Puts the server engines plugins register beside the built-in ones. An engine whose id another
/// already has is left out, with a warning.
/// </summary>
internal sealed class PluginServerEngines : IServerEngineProvider
{
    private readonly IReadOnlyList<PluginServerEngine> _engines;

    public PluginServerEngines(IEnumerable<IServerEngineExtension> extensions, IServiceProvider services, ILogger<PluginServerEngines> logger)
    {
        var engines = new List<PluginServerEngine>();

        foreach (var extension in extensions)
        {
            if (string.IsNullOrWhiteSpace(extension.Id) || extension.Id.Length > 128)
            {
                logger.LogWarning("The server engine {Engine} has no id, or one longer than 128 characters, so is not offered", extension.GetType().FullName);
                continue;
            }

            if (engines.Any(engine => string.Equals(engine.Extension.Id, extension.Id, StringComparison.OrdinalIgnoreCase)))
            {
                logger.LogWarning("Another server engine already has the id {EngineId}, so {Engine} is not offered", extension.Id, extension.GetType().FullName);
                continue;
            }

            engines.Add(ActivatorUtilities.CreateInstance<PluginServerEngine>(services, extension));
        }

        _engines = engines;
    }

    /// <summary>The plugins' engines, by their extensions.</summary>
    public IReadOnlyList<IServerEngineExtension> Extensions => _engines.Select(engine => engine.Extension).ToList();

    /// <summary>The engine with the given id, or null.</summary>
    public IServerEngineExtension? Find(string? id) =>
        _engines.FirstOrDefault(engine => string.Equals(engine.Extension.Id, id, StringComparison.OrdinalIgnoreCase))?.Extension;

    public IEnumerable<IServerEngine> GetEngines() => _engines;

    /// <summary>The consoles a server's engine offers for it; none when a plugin doesn't run it.</summary>
    public IReadOnlyList<ServerEngineConsole> GetConsoles(Guid serverId) => EngineFor(serverId)?.GetConsoles(serverId) ?? [];

    /// <summary>Reads one of a server's engine log consoles; see <see cref="IServerEngineExtension.ReadLogAsync"/>.</summary>
    public IAsyncEnumerable<ServerEngineLogLine> ReadLogAsync(Guid serverId, string consoleId, ServerEngineLogRequest request, CancellationToken cancellationToken) =>
        (EngineFor(serverId) ?? throw NotRunByPlugin()).ReadLogAsync(serverId, consoleId, request, cancellationToken);

    /// <summary>Opens one of a server's engine terminal consoles; see <see cref="IServerEngineExtension.OpenTerminalAsync"/>.</summary>
    public Task<IServerEngineTerminal> OpenTerminalAsync(Guid serverId, string consoleId, ServerEngineTerminalSize size, CancellationToken cancellationToken) =>
        (EngineFor(serverId) ?? throw NotRunByPlugin()).OpenTerminalAsync(serverId, consoleId, size, cancellationToken);

    private PluginServerEngine? EngineFor(Guid serverId) => _engines.FirstOrDefault(engine => engine.IsManaging(serverId));

    private static InvalidOperationException NotRunByPlugin() => new("The server isn't run by a plugin's engine.");

    /// <summary>A server as its engine sees it.</summary>
    public static ServerEngineServer ToEngineServer(ServerEntity server) => new()
    {
        Id = server.Id,
        Name = server.Name,
        GameId = server.GameId,
        Host = server.Host,
        Port = server.Port,
        Settings = server.EngineSettings,
    };
}

/// <summary>
/// Runs servers through a plugin's <see cref="IServerEngineExtension"/>, and the server's Before Start
/// and After Stop scripts around it.
/// </summary>
internal sealed class PluginServerEngine(
    IServerEngineExtension extension,
    IServiceScopeFactory scopeFactory,
    PowerShellScriptFactory powerShellScriptFactory,
    SdkMapper sdkMapper,
    IOptions<SDK.Models.Settings> settings,
    ILogger<PluginServerEngine> logger) : IServerEngine, IServerEngineHost
{
    // Status and process samples are polled every couple of seconds; one that hangs, e.g. on a host
    // that has gone away, mustn't hold up whoever is asking
    private static readonly TimeSpan SampleTimeout = TimeSpan.FromSeconds(10);

    private readonly ConcurrentDictionary<Guid, ServerEngineServer> _servers = new();

    public IServerEngineExtension Extension => extension;

    public event EventHandler<ServerStatusUpdateEventArgs>? OnServerStatusUpdate;
    public event EventHandler<ServerLogEventArgs>? OnServerLog;

    public Task InitializeAsync() => extension.InitializeAsync(this, CancellationToken.None);

    public async Task RefreshTrackingAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var serverService = scope.ServiceProvider.GetRequiredService<ServerService>();

        var servers = (await serverService.AsNoTracking().GetAsync(s => s.Engine == ServerEngine.Extension && s.EngineExtensionId == extension.Id))
            .Select(PluginServerEngines.ToEngineServer)
            .ToList();

        foreach (var server in servers)
            _servers[server.Id] = server;

        foreach (var id in _servers.Keys.Except(servers.Select(s => s.Id)).ToList())
            _servers.TryRemove(id, out _);

        await extension.ServersChangedAsync(servers, CancellationToken.None);
    }

    public bool IsManaging(Guid serverId) => _servers.ContainsKey(serverId);

    public async Task StartAsync(Guid serverId)
    {
        var server = await GetServerAsync(serverId);

        if (server == null || !_servers.TryGetValue(serverId, out var engineServer))
            return;

        Emit(server, ServerProcessStatus.Starting);

        logger.LogInformation("Starting server \"{ServerName}\" with the {Engine} engine", server.Name, extension.Name);

        await RunScriptsAsync(server, ScriptType.BeforeStart);

        try
        {
            await extension.StartAsync(engineServer, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Emit(server, ServerProcessStatus.Error, ex);

            throw;
        }

        Emit(server, ServerProcessStatus.Running);
    }

    public async Task StopAsync(Guid serverId)
    {
        var server = await GetServerAsync(serverId);

        if (server == null || !_servers.TryGetValue(serverId, out var engineServer))
            return;

        Emit(server, ServerProcessStatus.Stopping);

        logger.LogInformation("Stopping server \"{ServerName}\" with the {Engine} engine", server.Name, extension.Name);

        try
        {
            await extension.StopAsync(engineServer, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Emit(server, ServerProcessStatus.Error, ex);

            throw;
        }

        await RunScriptsAsync(server, ScriptType.AfterStop);

        Emit(server, ServerProcessStatus.Stopped);
    }

    public async Task<ServerProcessStatus> GetStatusAsync(Guid serverId)
    {
        if (!_servers.TryGetValue(serverId, out var server))
            return ServerProcessStatus.Stopped;

        try
        {
            using var timeout = new CancellationTokenSource(SampleTimeout);

            return ToProcessStatus(await extension.GetStatusAsync(server, timeout.Token));
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "The {Engine} engine could not tell the status of server {ServerId}", extension.Name, serverId);

            return ServerProcessStatus.Error;
        }
    }

    public async Task<ServerProcessInfo?> GetProcessInfoAsync(Guid serverId)
    {
        if (!extension.Features.HasFlag(ServerEngineFeatures.ProcessInfo) || !_servers.TryGetValue(serverId, out var server))
            return null;

        using var timeout = new CancellationTokenSource(SampleTimeout);

        var info = await extension.GetProcessInfoAsync(server, timeout.Token);

        return info == null ? null : new ServerProcessInfo(info.Pid, info.StartTime, info.CpuPercent, info.MemoryBytes);
    }

    public bool CanSendInput(Guid serverId)
    {
        if (!extension.Features.HasFlag(ServerEngineFeatures.Input) || !_servers.TryGetValue(serverId, out var server))
            return false;

        try
        {
            return extension.CanSendInput(server);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "The {Engine} engine could not tell whether server {ServerId} takes input", extension.Name, serverId);

            return false;
        }
    }

    public async Task<bool> SendInputAsync(Guid serverId, string line)
    {
        if (!extension.Features.HasFlag(ServerEngineFeatures.Input) || !_servers.TryGetValue(serverId, out var server))
            return false;

        return await extension.SendInputAsync(server, line, CancellationToken.None);
    }

    public IReadOnlyList<ServerEngineConsole> GetConsoles(Guid serverId)
    {
        if (!extension.Features.HasFlag(ServerEngineFeatures.Consoles) || !_servers.TryGetValue(serverId, out var server))
            return [];

        try
        {
            return extension.GetConsoles(server)
                .Where(console => !string.IsNullOrWhiteSpace(console?.Id))
                .DistinctBy(console => console.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "The {Engine} engine could not list the consoles of server {ServerId}", extension.Name, serverId);

            return [];
        }
    }

    public IAsyncEnumerable<ServerEngineLogLine> ReadLogAsync(Guid serverId, string consoleId, ServerEngineLogRequest request, CancellationToken cancellationToken)
    {
        var (server, console) = FindConsole(serverId, consoleId, ServerEngineConsoleKind.Log);

        return extension.ReadLogAsync(server, console.Id, request, cancellationToken);
    }

    public async Task<IServerEngineTerminal> OpenTerminalAsync(Guid serverId, string consoleId, ServerEngineTerminalSize size, CancellationToken cancellationToken)
    {
        var (server, console) = FindConsole(serverId, consoleId, ServerEngineConsoleKind.Terminal);

        return await extension.OpenTerminalAsync(server, console.Id, size, cancellationToken)
            ?? throw new InvalidOperationException($"The {extension.Name} engine didn't open the terminal.");
    }

    // Only consoles the engine lists are opened, so an address typed by hand can't reach anything else
    private (ServerEngineServer Server, ServerEngineConsole Console) FindConsole(Guid serverId, string consoleId, ServerEngineConsoleKind kind)
    {
        if (!_servers.TryGetValue(serverId, out var server))
            throw new InvalidOperationException($"The server isn't run by the {extension.Name} engine.");

        var console = GetConsoles(serverId).FirstOrDefault(console => console.Kind == kind && string.Equals(console.Id, consoleId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The server has no such console.");

        return (server, console);
    }

    public bool SupportsFiles(Guid serverId) => extension.Features.HasFlag(ServerEngineFeatures.Files) && _servers.ContainsKey(serverId);

    public async Task<IReadOnlyList<ServerFile>> ListFilesAsync(Guid serverId, string path, CancellationToken cancellationToken = default)
    {
        var files = await extension.ListFilesAsync(FilesOf(serverId), path, cancellationToken) ?? [];

        // Only names within the folder; anything else the engine lists couldn't be reached through it
        return files
            .Where(file => !string.IsNullOrWhiteSpace(file?.Name) && file.Name is not ("." or "..") && file.Name.IndexOfAny(['/', '\\']) < 0)
            .DistinctBy(file => file.Name)
            .Select(file => new ServerFile(file.Name, ServerFilePath.Combine(path, file.Name), file.IsDirectory, file.IsDirectory ? null : file.Size, file.LastModified))
            .ToList();
    }

    public async Task<Stream> OpenFileAsync(Guid serverId, string path, CancellationToken cancellationToken = default) =>
        await extension.OpenFileAsync(FilesOf(serverId), path, cancellationToken)
            ?? throw new InvalidOperationException($"The {extension.Name} engine didn't open the file.");

    public Task WriteFileAsync(Guid serverId, string path, Stream content, CancellationToken cancellationToken = default) =>
        extension.WriteFileAsync(FilesOf(serverId), path, content, cancellationToken);

    public Task DeleteFileAsync(Guid serverId, string path, CancellationToken cancellationToken = default) =>
        extension.DeleteFileAsync(FilesOf(serverId), path, cancellationToken);

    public Task CreateDirectoryAsync(Guid serverId, string path, CancellationToken cancellationToken = default) =>
        extension.CreateDirectoryAsync(FilesOf(serverId), path, cancellationToken);

    private ServerEngineServer FilesOf(Guid serverId)
    {
        if (!_servers.TryGetValue(serverId, out var server))
            throw new InvalidOperationException($"The server isn't run by the {extension.Name} engine.");

        if (!extension.Features.HasFlag(ServerEngineFeatures.Files))
            throw new NotSupportedException($"The {extension.Name} engine can't manage server files.");

        return server;
    }

    void IServerEngineHost.ReportStatus(Guid serverId, ServerEngineStatus status, string? error)
    {
        if (!_servers.TryGetValue(serverId, out var server))
            return;

        // Those listening only want the id, and the name for messages
        Emit(new ServerEntity { Id = server.Id, Name = server.Name }, ToProcessStatus(status), error == null ? null : new Exception(error));
    }

    void IServerEngineHost.WriteOutput(Guid serverId, string line)
    {
        if (line == null || !_servers.ContainsKey(serverId))
            return;

        try
        {
            OnServerLog?.Invoke(this, new ServerLogEventArgs(serverId, line));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not publish output from server {ServerId}", serverId);
        }
    }

    private void Emit(ServerEntity server, ServerProcessStatus status, Exception? ex = null)
    {
        try
        {
            OnServerStatusUpdate?.Invoke(this, ex == null
                ? new ServerStatusUpdateEventArgs(server, status)
                : new ServerStatusUpdateEventArgs(server, ServerProcessStatus.Error, ex));
        }
        catch (Exception handlerException)
        {
            logger.LogError(handlerException, "Could not publish the status of server {ServerId}", server.Id);
        }
    }

    private async Task<ServerEntity?> GetServerAsync(Guid serverId)
    {
        using var scope = scopeFactory.CreateScope();
        var serverService = scope.ServiceProvider.GetRequiredService<ServerService>();

        return await serverService
            .AsNoTracking()
            .Query(q => q
                .Include(s => s.Scripts)
                .Include(s => s.Game))
            .GetAsync(serverId);
    }

    private async Task RunScriptsAsync(ServerEntity server, ScriptType type)
    {
        foreach (var serverScript in server.Scripts?.Where(s => s.Type == type) ?? [])
        {
            try
            {
                var script = powerShellScriptFactory.Create(type);

                script.AddVariable("Server", sdkMapper.ToSdk(server));

                if (!string.IsNullOrWhiteSpace(server.WorkingDirectory))
                    script.UseWorkingDirectory(server.WorkingDirectory);

                script.UseInline(serverScript.Contents);
                script.UseShellExecute();

                logger.LogInformation("Executing script \"{ScriptName}\"", serverScript.Name);

                if (settings.Value.Debug.EnableScriptDebugging)
                    script.EnableDebug();

                await script.ExecuteAsync<int>();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error running script \"{ScriptName}\" for server \"{ServerName}\"", serverScript.Name, server.Name);
            }
        }
    }

    private static ServerProcessStatus ToProcessStatus(ServerEngineStatus status) => status switch
    {
        ServerEngineStatus.Starting => ServerProcessStatus.Starting,
        ServerEngineStatus.Running => ServerProcessStatus.Running,
        ServerEngineStatus.Stopping => ServerProcessStatus.Stopping,
        ServerEngineStatus.Error => ServerProcessStatus.Error,
        _ => ServerProcessStatus.Stopped,
    };
}
