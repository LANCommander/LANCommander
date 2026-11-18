using LANCommander.Server.Plugins;
using LANCommander.Server.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services.ServerSources;

/// <summary>
/// The <see cref="IServerSourceExtension"/>s, built-in and from plugins, each run as an engine beside the
/// others so their servers can be started, stopped and watched like the server's own. A source whose id
/// another already has is left out, with a warning.
/// </summary>
public sealed class ServerSourceRegistry : IServerEngineProvider, IAsyncDisposable
{
    private readonly IReadOnlyList<ServerSourceEngine> _engines;

    public ServerSourceRegistry(IEnumerable<IServerSourceExtension> sources, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger<ServerSourceRegistry>();
        var engines = new List<ServerSourceEngine>();

        foreach (var source in sources)
        {
            if (string.IsNullOrWhiteSpace(source.Id) || source.Id.Length > 128)
            {
                logger.LogWarning("The server source {Source} has no id, or one longer than 128 characters, so its servers are not listed", source.GetType().FullName);
                continue;
            }

            if (engines.Any(engine => string.Equals(engine.Source.Id, source.Id, StringComparison.OrdinalIgnoreCase)))
            {
                logger.LogWarning("Another server source already has the id {SourceId}, so {Source}'s servers are not listed", source.Id, source.GetType().FullName);
                continue;
            }

            engines.Add(new ServerSourceEngine(source, loggerFactory.CreateLogger($"{typeof(ServerSourceEngine).FullName}[{source.Id}]")));
        }

        _engines = engines;
    }

    public IEnumerable<IServerEngine> GetEngines() => _engines;

    /// <summary>Every source's servers as last listed, by source then name.</summary>
    public IReadOnlyList<ExternalServerListing> GetServers() => _engines
        .OrderBy(engine => engine.Source.Name, StringComparer.CurrentCultureIgnoreCase)
        .SelectMany(engine => engine.Servers)
        .ToList();

    /// <summary>The listed server with the given id, or null when no source lists it.</summary>
    public ExternalServerListing? Find(Guid serverId)
    {
        foreach (var engine in _engines)
        {
            if (engine.Find(serverId) is { } server)
                return server;
        }

        return null;
    }

    /// <summary>The engine running the listed server with the given id, or null.</summary>
    public ServerSourceEngine? EngineFor(Guid serverId) => _engines.FirstOrDefault(engine => engine.IsManaging(serverId));

    /// <summary>The source's name for the listed server with the given id, or null.</summary>
    public string? SourceNameFor(Guid serverId) => EngineFor(serverId)?.Source.Name;

    /// <summary>Lists every source's servers again now.</summary>
    public Task RefreshAsync(CancellationToken cancellationToken = default) =>
        Task.WhenAll(_engines.Select(engine => engine.RefreshAsync(cancellationToken)));

    public async ValueTask DisposeAsync()
    {
        foreach (var engine in _engines)
            await engine.DisposeAsync();
    }
}
