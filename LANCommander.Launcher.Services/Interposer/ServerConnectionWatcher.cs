using System.Net;
using LANCommander.Interposer;
using LANCommander.Interposer.Events;
using LANCommander.SDK.Services;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services.Interposer;

/// <summary>
/// Watches the addresses a game running under the Interposer talks to and tells the server when one
/// is a game server it manages for the game. That ties the player's session, and so their part in
/// that server's matches, to the server even when they joined from the game's own server browser.
/// </summary>
/// <remarks>
/// Only the game's own servers are ever reported; any other address the game talks to never leaves
/// the launcher.
/// </remarks>
public sealed class ServerConnectionWatcher(GameClient gameClient, ILogger<ServerConnectionWatcher> logger)
{
    /// <summary>A game server the game could join, with the addresses its host resolves to.</summary>
    public sealed record KnownServer(Guid Id, string Host, IReadOnlyList<IPAddress> Addresses, int Port);

    /// <summary>Watches until the result is disposed, when the game has exited.</summary>
    public IDisposable Track(Guid gameId, InterposerService interposer) =>
        new Tracker(gameId, interposer, this);

    /// <summary>
    /// The server at <paramref name="address"/> and <paramref name="port"/>, if any. A server with no
    /// port matches any port, and an unknown port (0) matches a server only on its address.
    /// </summary>
    public static Guid? Match(IEnumerable<KnownServer> servers, string address, int port)
    {
        var ip = IPAddress.TryParse(address, out var parsed) ? Normalize(parsed) : null;

        foreach (var server in servers)
        {
            var addressMatches = ip != null
                ? server.Addresses.Any(a => Normalize(a).Equals(ip))
                : string.Equals(server.Host, address, StringComparison.OrdinalIgnoreCase);

            if (!addressMatches)
                continue;

            if (server.Port == 0 || port == 0 || server.Port == port)
                return server.Id;
        }

        return null;
    }

    // ::ffff:10.0.0.5 and 10.0.0.5 are the same server
    private static IPAddress Normalize(IPAddress address) =>
        address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

    private Task ReportAsync(Guid gameId, Guid serverId)
    {
        logger.LogInformation("Game {GameId} connected to game server {ServerId}", gameId, serverId);

        return gameClient.ServerConnectedAsync(gameId, serverId);
    }

    private async Task<IReadOnlyList<KnownServer>> LoadServersAsync(Guid gameId)
    {
        var known = new List<KnownServer>();

        try
        {
            foreach (var server in await gameClient.GetServersAsync(gameId) ?? [])
            {
                if (string.IsNullOrWhiteSpace(server.Host))
                    continue;

                IReadOnlyList<IPAddress> addresses;

                if (IPAddress.TryParse(server.Host, out var literal))
                    addresses = [literal];
                else
                {
                    try
                    {
                        addresses = await Dns.GetHostAddressesAsync(server.Host);
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, "Couldn't resolve game server {Host}", server.Host);
                        addresses = [];
                    }
                }

                known.Add(new KnownServer(server.Id, server.Host, addresses, server.Port));
            }
        }
        catch (Exception ex)
        {
            // Offline, or a server too old to list a game's servers: nothing to watch for
            logger.LogDebug(ex, "Couldn't list the game servers of game {GameId}", gameId);
        }

        return known;
    }

    private sealed class Tracker : IDisposable
    {
        private readonly Guid _gameId;
        private readonly InterposerService _interposer;
        private readonly ServerConnectionWatcher _watcher;
        private readonly object _lock = new();
        private readonly List<(string Address, int Port)> _early = [];
        private readonly HashSet<Guid> _reported = [];
        private IReadOnlyList<KnownServer>? _servers;
        private bool _disposed;

        public Tracker(Guid gameId, InterposerService interposer, ServerConnectionWatcher watcher)
        {
            _gameId = gameId;
            _interposer = interposer;
            _watcher = watcher;

            _interposer.NetworkConnection += OnNetworkConnection;

            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            var servers = await _watcher.LoadServersAsync(_gameId);

            List<(string, int)> early;

            lock (_lock)
            {
                _servers = servers;
                early = [.. _early];
                _early.Clear();
            }

            // Connections the game made while the list was loading
            foreach (var (address, port) in early)
                Check(address, port);
        }

        private void OnNetworkConnection(object? sender, NetworkEventArgs e)
        {
            lock (_lock)
            {
                if (_servers == null)
                {
                    _early.Add((e.Address, e.Port));
                    return;
                }
            }

            Check(e.Address, e.Port);
        }

        private void Check(string address, int port)
        {
            Guid serverId;

            lock (_lock)
            {
                if (_disposed || _servers == null || _servers.Count == 0)
                    return;

                if (Match(_servers, address, port) is not { } matched || !_reported.Add(matched))
                    return;

                serverId = matched;
            }

            _ = _watcher.ReportAsync(_gameId, serverId);
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                    return;

                _disposed = true;
            }

            _interposer.NetworkConnection -= OnNetworkConnection;
        }
    }
}
