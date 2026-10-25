using System.Collections.Concurrent;
using LANCommander.Interposer;
using LANCommander.Interposer.Events;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services.Interposer;

/// <summary>
/// The games running under the Interposer, so the rest of the launcher can see and steer what the
/// Interposer is doing in them -- for now, the network relay. <see cref="InterposerGameProcessStarter"/>
/// registers each game it starts; the registration ends when the game exits.
/// </summary>
public sealed class InterposerSessionService(ILogger<InterposerSessionService> logger)
{
    private readonly ConcurrentDictionary<Guid, InterposerSession> _sessions = new();

    /// <summary>The game's session, or null when it is not running under the Interposer.</summary>
    public InterposerSession? Get(Guid gameId) => _sessions.TryGetValue(gameId, out var session) ? session : null;

    /// <summary>Records a game the Interposer started. Dispose the result when the game has exited.</summary>
    /// <param name="gameId">The game.</param>
    /// <param name="interposer">The service that started it, which holds its control pipe.</param>
    /// <param name="joinsRelay">Whether it was started to join the network relay.</param>
    /// <param name="renewRelay">
    /// A fresh relay session for a reconnect, or null to reconnect with the one it has: a session's ticket
    /// expires, and a game can outlast it.
    /// </param>
    public IDisposable Register(
        Guid gameId,
        InterposerService interposer,
        bool joinsRelay,
        Func<CancellationToken, Task<RelayOptions?>>? renewRelay = null)
    {
        var session = new InterposerSession(gameId, interposer, joinsRelay, renewRelay, logger);

        _sessions[gameId] = session;

        return new Registration(() =>
        {
            // Only this session's own entry: a game started again may already have replaced it
            _sessions.TryRemove(new KeyValuePair<Guid, InterposerSession>(gameId, session));
            session.Detach();
        });
    }

    private sealed class Registration(Action release) : IDisposable
    {
        private Action? _release = release;

        public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
    }
}

/// <summary>A player in the relay network, other than this one.</summary>
public sealed record RelayPlayer(uint ClientId, string Name, string VirtualAddress);

/// <summary>The relay as one game sees it at a moment.</summary>
/// <param name="State">The connection state.</param>
/// <param name="VirtualAddress">This player's address on the virtual LAN, empty before the first connection.</param>
/// <param name="Players">The other players in the network, by name.</param>
public sealed record RelaySnapshot(RelayState State, string VirtualAddress, IReadOnlyList<RelayPlayer> Players);

/// <summary>
/// One game running under the Interposer. Follows its relay from the events the DLL sends -- state
/// changes, and players joining and leaving, which it also sends once for every player already there
/// when it connects -- rather than by asking, so reading it never touches the game.
/// </summary>
public sealed class InterposerSession
{
    private readonly InterposerService _interposer;
    private readonly Func<CancellationToken, Task<RelayOptions?>>? _renewRelay;
    private readonly ILogger _logger;
    private readonly object _lock = new();
    private readonly Dictionary<uint, RelayPlayer> _players = new();

    private RelayState _state;
    private string _virtualAddress = string.Empty;

    internal InterposerSession(
        Guid gameId,
        InterposerService interposer,
        bool joinsRelay,
        Func<CancellationToken, Task<RelayOptions?>>? renewRelay,
        ILogger logger)
    {
        GameId = gameId;
        JoinsRelay = joinsRelay;

        _interposer = interposer;
        _renewRelay = renewRelay;
        _logger = logger;
        _state = joinsRelay ? RelayState.Connecting : RelayState.Disabled;

        interposer.RelayStateChanged += OnRelayStateChanged;
        interposer.RelayPeerJoined += OnRelayPeerJoined;
        interposer.RelayPeerLeft += OnRelayPeerLeft;
    }

    public Guid GameId { get; }

    /// <summary>Whether the game was started to join the network relay.</summary>
    public bool JoinsRelay { get; }

    /// <summary>Raised on a background thread whenever the relay's state or players change.</summary>
    public event EventHandler? Changed;

    public RelaySnapshot Relay
    {
        get
        {
            lock (_lock)
                return new RelaySnapshot(_state, _virtualAddress, _players.Values.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList());
        }
    }

    /// <summary>Takes the game off the relay until <see cref="ReconnectRelayAsync"/>. It keeps its address.</summary>
    public async Task DisconnectRelayAsync(CancellationToken cancellationToken = default)
    {
        await _interposer.DisconnectRelayAsync(cancellationToken);

        // The DLL reports the state too; this is so the change shows without waiting for it
        Apply(RelayState.Disabled, null);
    }

    /// <summary>Puts the game back on the relay, with a fresh session when one can be had.</summary>
    public async Task<bool> ReconnectRelayAsync(CancellationToken cancellationToken = default)
    {
        RelayOptions? options = null;

        if (_renewRelay != null)
        {
            try
            {
                options = await _renewRelay(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Could not get a new relay session for game {GameId}; reconnecting with the one it has", GameId);
            }
        }

        Apply(RelayState.Connecting, null);

        return await _interposer.ConnectRelayAsync(options, cancellationToken);
    }

    /// <summary>Stops following the game, which has exited.</summary>
    internal void Detach()
    {
        _interposer.RelayStateChanged -= OnRelayStateChanged;
        _interposer.RelayPeerJoined -= OnRelayPeerJoined;
        _interposer.RelayPeerLeft -= OnRelayPeerLeft;
    }

    private void OnRelayStateChanged(object? sender, RelayStateChangedEventArgs e) => Apply(e.State, e.VirtualAddress);

    private void OnRelayPeerJoined(object? sender, RelayPeerEventArgs e) => ApplyPlayer(ToPlayer(e), joined: true);

    private void OnRelayPeerLeft(object? sender, RelayPeerEventArgs e) => ApplyPlayer(ToPlayer(e), joined: false);

    // A player whose name did not come through is shown by address
    private static RelayPlayer ToPlayer(RelayPeerEventArgs e) =>
        new(e.ClientId, string.IsNullOrWhiteSpace(e.Name) ? e.VirtualAddress : e.Name, e.VirtualAddress);

    internal void Apply(RelayState state, string? virtualAddress)
    {
        lock (_lock)
        {
            _state = state;

            if (!string.IsNullOrEmpty(virtualAddress))
                _virtualAddress = virtualAddress;

            // Off the relay, nobody else is in the network as far as this game knows
            if (state is RelayState.Disabled or RelayState.Failed)
                _players.Clear();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    internal void ApplyPlayer(RelayPlayer player, bool joined)
    {
        lock (_lock)
        {
            if (joined)
                _players[player.ClientId] = player;
            else
                _players.Remove(player.ClientId);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
