using LANCommander.Server.Data.Models;
using LANCommander.Server.Data;
using ServerEntity = LANCommander.Server.Data.Models.Server;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Utilities;
using LANCommander.Server.Settings.Enums;
using Microsoft.EntityFrameworkCore;

namespace LANCommander.Server.Plugins.Records;

/// <summary>Gives plugins the server's game servers without exposing the database model.</summary>
internal sealed class ServerProvider(
    ServerService servers,
    ServerConsoleService consoles,
    ServerHttpPathService httpPaths,
    GameService games,
    ServerManager serverManager,
    IDbContextFactory<DatabaseContext> contextFactory) : IServerProvider
{
    public async Task<IReadOnlyList<ServerFileInfo>> ListFilesAsync(Guid serverId, string relativePath, CancellationToken cancellationToken = default)
    {
        var path = await RequireFilesAsync(serverId, relativePath, false, cancellationToken);
        var files = await serverManager.ListFilesAsync(serverId, path, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        return files.Select(file => new ServerFileInfo(file.Name, file.Path, file.IsDirectory, file.Size, file.LastModified)).ToList();
    }

    public async Task<Stream> OpenFileAsync(Guid serverId, string relativePath, CancellationToken cancellationToken = default)
    {
        var path = await RequireFilesAsync(serverId, relativePath, true, cancellationToken);
        var stream = await serverManager.OpenFileAsync(serverId, path, cancellationToken);

        if (cancellationToken.IsCancellationRequested)
        {
            await stream.DisposeAsync();
            cancellationToken.ThrowIfCancellationRequested();
        }

        return stream;
    }

    public async Task WriteFileAsync(Guid serverId, string relativePath, Stream content, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(content);

        if (!content.CanRead)
            throw new ArgumentException("The content stream must be readable.", nameof(content));

        var path = await RequireFilesAsync(serverId, relativePath, true, cancellationToken);

        await serverManager.WriteFileAsync(serverId, path, content, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task<string> RequireFilesAsync(Guid serverId, string relativePath, bool requireFile, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(relativePath);

        // The file manager also accepts root-looking paths for navigation; plugins promise relative
        // paths, so reject those and platform-specific aliases before using its shared normalizer.
        if (relativePath.StartsWith('/') || relativePath.StartsWith('\\')
            || relativePath.Any(char.IsControl)
            || relativePath.Split(['/', '\\']).Any(segment =>
                segment is not ("" or ".") && (segment.EndsWith('.') || segment.EndsWith(' ')
                    || segment.IndexOfAny(['*', '?', '"', '<', '>', '|']) >= 0)))
            throw new ArgumentException("A relative server file path is required.", nameof(relativePath));

        var path = ServerFilePath.Normalize(relativePath);

        if (requireFile && path.Length == 0)
            throw new ArgumentException("A file must be named.", nameof(relativePath));

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        if (!await context.Set<ServerEntity>().AnyAsync(server => server.Id == serverId, cancellationToken))
            throw RecordGuard.NotFound("server", serverId);

        cancellationToken.ThrowIfCancellationRequested();

        return path;
    }

    public async Task<IReadOnlyList<ServerInfo>> GetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return (await servers.GetAsync())
            .Select(Info)
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<ServerInfo?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var server = await servers.GetAsync(id);

        return server == null ? null : Info(server);
    }

    public async Task<ServerInfo> AddAsync(ServerInfo server, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await CheckReferencesAsync(server);

        var entity = new ServerEntity { Id = server.Id };

        Apply(server, entity);

        var added = await servers.AddAsync(entity);

        return Info(RecordGuard.Found(await servers.GetAsync(added.Id), "server", added.Id));
    }

    public async Task<ServerInfo> UpdateAsync(ServerInfo server, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entity = RecordGuard.Found(await servers.Include(s => s.CreatedBy!).GetAsync(server.Id), "server", server.Id);

        await CheckReferencesAsync(server);

        Apply(server, entity);

        await servers.UpdateAsync(entity);

        return Info(RecordGuard.Found(await servers.GetAsync(server.Id), "server", server.Id));
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var server = await servers.GetAsync(id);

        if (server != null)
            await servers.DeleteAsync(server);
    }

    public async Task<IReadOnlyList<ServerConsoleInfo>> GetConsolesAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await RequireServerAsync(serverId);

        return (await consoles.GetAsync(c => c.ServerId == serverId))
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(ConsoleInfo)
            .ToList();
    }

    public async Task<ServerConsoleInfo> AddConsoleAsync(Guid serverId, ServerConsoleInfo console, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await RequireServerAsync(serverId);

        var entity = new ServerConsole { Id = console.Id, ServerId = serverId };

        Apply(console, entity);

        var added = await consoles.AddAsync(entity);

        return ConsoleInfo(RecordGuard.Found(await consoles.GetAsync(added.Id), "console", added.Id));
    }

    public async Task<ServerConsoleInfo> UpdateConsoleAsync(ServerConsoleInfo console, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entity = RecordGuard.Found(await consoles.Include(c => c.CreatedBy!).GetAsync(console.Id), "console", console.Id);

        Apply(console, entity);

        await consoles.UpdateAsync(entity);

        return ConsoleInfo(RecordGuard.Found(await consoles.GetAsync(console.Id), "console", console.Id));
    }

    public async Task DeleteConsoleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var console = await consoles.GetAsync(id);

        if (console != null)
            await consoles.DeleteAsync(console);
    }

    public async Task<IReadOnlyList<ServerHttpPathInfo>> GetHttpPathsAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await RequireServerAsync(serverId);

        return (await httpPaths.GetAsync(p => p.ServerId == serverId))
            .OrderBy(p => p.Path, StringComparer.OrdinalIgnoreCase)
            .Select(HttpPathInfo)
            .ToList();
    }

    public async Task<ServerHttpPathInfo> AddHttpPathAsync(Guid serverId, ServerHttpPathInfo httpPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await RequireServerAsync(serverId);

        var entity = new ServerHttpPath { Id = httpPath.Id, ServerId = serverId };

        Apply(httpPath, entity);

        var added = await httpPaths.AddAsync(entity);

        return HttpPathInfo(RecordGuard.Found(await httpPaths.GetAsync(added.Id), "HTTP path", added.Id));
    }

    public async Task<ServerHttpPathInfo> UpdateHttpPathAsync(ServerHttpPathInfo httpPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entity = RecordGuard.Found(await httpPaths.Include(p => p.CreatedBy!).GetAsync(httpPath.Id), "HTTP path", httpPath.Id);

        Apply(httpPath, entity);

        await httpPaths.UpdateAsync(entity);

        return HttpPathInfo(RecordGuard.Found(await httpPaths.GetAsync(httpPath.Id), "HTTP path", httpPath.Id));
    }

    public async Task DeleteHttpPathAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var httpPath = await httpPaths.GetAsync(id);

        if (httpPath != null)
            await httpPaths.DeleteAsync(httpPath);
    }

    async Task RequireServerAsync(Guid serverId)
    {
        if (!await servers.ExistsAsync(serverId))
            throw RecordGuard.NotFound("server", serverId);
    }

    async Task CheckReferencesAsync(ServerInfo server)
    {
        if (string.IsNullOrWhiteSpace(server.Name))
            throw new ArgumentException("A server needs a name.", nameof(server));

        if (server.GameId is { } gameId && gameId != Guid.Empty && !await games.ExistsAsync(gameId))
            throw RecordGuard.NotFound("game", gameId);
    }

    static void Apply(ServerInfo info, ServerEntity entity)
    {
        entity.Name = info.Name.Trim();
        entity.GameId = info.GameId == Guid.Empty ? null : info.GameId;
        entity.Engine = info.Engine switch
        {
            ServerHostEngine.Remote => ServerEngine.Remote,
            ServerHostEngine.Extension => ServerEngine.Extension,
            _ => ServerEngine.Local,
        };
        entity.Path = info.Path ?? "";
        entity.Arguments = info.Arguments ?? "";
        entity.WorkingDirectory = info.WorkingDirectory ?? "";
        entity.OnStartScriptPath = info.OnStartScriptPath ?? "";
        entity.OnStopScriptPath = info.OnStopScriptPath ?? "";
        entity.UseShellExecute = info.UseShellExecute;
        entity.ProcessTerminationMethod = info.ProcessTerminationMethod;
        entity.EngineExtensionId = info.EngineExtensionId ?? "";
        entity.EngineSettings = info.EngineSettings ?? "";
        entity.RemoteHostId = info.RemoteHostId;
        entity.RemoteServerId = info.RemoteServerId;
        entity.Host = info.Host ?? "";
        entity.Port = info.Port;
        entity.Autostart = info.Autostart;
        entity.AutostartMethod = info.AutostartMethod;
        entity.AutostartDelay = info.AutostartDelay;
    }

    static void Apply(ServerConsoleInfo info, ServerConsole entity)
    {
        if (string.IsNullOrWhiteSpace(info.Name))
            throw new ArgumentException("A console needs a name.", nameof(info));

        entity.Name = info.Name.Trim();
        entity.Type = info.Type;
        entity.Path = info.Path ?? "";
        entity.Host = info.Host ?? "";
        entity.Port = info.Port;
        entity.Password = info.Password ?? "";
    }

    static void Apply(ServerHttpPathInfo info, ServerHttpPath entity)
    {
        if (string.IsNullOrWhiteSpace(info.LocalPath) || string.IsNullOrWhiteSpace(info.Path))
            throw new ArgumentException("An HTTP path needs a local path and a URL path.", nameof(info));

        entity.LocalPath = info.LocalPath;
        entity.Path = info.Path;
    }

    static ServerInfo Info(ServerEntity entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        GameId = entity.GameId,
        Engine = entity.Engine switch
        {
            ServerEngine.Remote => ServerHostEngine.Remote,
            ServerEngine.Extension => ServerHostEngine.Extension,
            _ => ServerHostEngine.Local,
        },
        Path = entity.Path,
        Arguments = entity.Arguments,
        WorkingDirectory = entity.WorkingDirectory,
        OnStartScriptPath = entity.OnStartScriptPath,
        OnStopScriptPath = entity.OnStopScriptPath,
        UseShellExecute = entity.UseShellExecute,
        ProcessTerminationMethod = entity.ProcessTerminationMethod,
        EngineExtensionId = entity.EngineExtensionId,
        EngineSettings = entity.EngineSettings,
        RemoteHostId = entity.RemoteHostId,
        RemoteServerId = entity.RemoteServerId,
        Host = entity.Host,
        Port = entity.Port,
        Autostart = entity.Autostart,
        AutostartMethod = entity.AutostartMethod,
        AutostartDelay = entity.AutostartDelay,
        CreatedOn = entity.CreatedOn,
        UpdatedOn = entity.UpdatedOn,
    };

    static ServerConsoleInfo ConsoleInfo(ServerConsole entity) => new()
    {
        Id = entity.Id,
        ServerId = entity.ServerId,
        Name = entity.Name,
        Type = entity.Type,
        Path = entity.Path,
        Host = entity.Host,
        Port = entity.Port,
        Password = entity.Password,
    };

    static ServerHttpPathInfo HttpPathInfo(ServerHttpPath entity) => new()
    {
        Id = entity.Id,
        ServerId = entity.ServerId,
        LocalPath = entity.LocalPath,
        Path = entity.Path,
    };
}
