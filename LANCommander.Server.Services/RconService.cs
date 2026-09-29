using System.Collections.Concurrent;
using LANCommander.SDK.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Models;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services;

/// <summary>Sends commands to a game server's RCON console.</summary>
public interface IRconCommandSender
{
    /// <summary>Whether the console is an RCON console with enough configuration to attempt a connection.</summary>
    bool CanSend(ServerConsole console);

    /// <summary>Sends a command and returns the server's response.</summary>
    Task<string> SendCommandAsync(ServerConsole console, string command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Singleton pool of <see cref="RconConnection"/>s, one per RCON console. A connection is
/// recreated when the console's host, port or password changes, and after a failed command.
/// </summary>
public sealed class RconService(ILogger<RconService> logger) : IRconCommandSender, IDisposable
{
    private readonly ConcurrentDictionary<Guid, (string Key, RconConnection Connection)> _connections = new();

    public bool CanSend(ServerConsole console) =>
        console is { Type: ServerConsoleType.RCON, Port: > 0 and <= 65535 }
        && !String.IsNullOrWhiteSpace(console.Host);

    public async Task<string> SendCommandAsync(ServerConsole console, string command, CancellationToken cancellationToken = default)
    {
        if (!CanSend(console))
            throw new InvalidOperationException("The console is not a configured RCON console");

        var connection = GetConnection(console);

        try
        {
            return await connection.SendCommandAsync(command, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "RCON command to {Host}:{Port} failed", console.Host, console.Port);

            // Drop the broken connection so the next command reconnects from scratch
            if (_connections.TryRemove(console.Id, out var entry))
                entry.Connection.Dispose();

            throw;
        }
    }

    private RconConnection GetConnection(ServerConsole console)
    {
        var key = $"{console.Host}:{console.Port}:{console.Password}";

        while (true)
        {
            if (_connections.TryGetValue(console.Id, out var existing))
            {
                if (existing.Key == key)
                    return existing.Connection;

                if (_connections.TryRemove(new KeyValuePair<Guid, (string, RconConnection)>(console.Id, existing)))
                    existing.Connection.Dispose();

                continue;
            }

            var connection = new RconConnection(console.Host, console.Port!.Value, console.Password ?? "");

            if (_connections.TryAdd(console.Id, (key, connection)))
                return connection;

            connection.Dispose();
        }
    }

    public void Dispose()
    {
        foreach (var entry in _connections.Values)
            entry.Connection.Dispose();

        _connections.Clear();
    }
}
