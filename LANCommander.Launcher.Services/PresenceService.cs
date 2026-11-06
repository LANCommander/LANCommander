using System.Collections.Concurrent;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;
using LANCommander.SDK.Services;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services;

/// <summary>
/// Who on the server is playing what right now, kept current from what the server pushes. The
/// views read it and redraw on <see cref="Changed"/>.
/// </summary>
public sealed class PresenceService : IDisposable
{
    private readonly PresenceClient _client;
    private readonly ILogger<PresenceService> _logger;
    private readonly ConcurrentDictionary<Guid, UserPresence> _byUser = new();
    private int _refreshGeneration;

    public PresenceService(PresenceClient client, ILogger<PresenceService> logger)
    {
        _client = client;
        _logger = logger;

        _client.PresenceChanged += OnPresenceChanged;
        _client.PresenceEnded += OnPresenceEnded;
        _client.Connected += OnConnected;
        _client.Disconnected += OnDisconnected;
    }

    /// <summary>
    /// Raised off the UI thread when presence changes: with the user whose presence changed, or
    /// null when everything may have.
    /// </summary>
    public event EventHandler<Guid?>? Changed;

    /// <summary>Raised off the UI thread when this user's own visibility is known or changes.</summary>
    public event EventHandler? VisibilityChanged;

    /// <summary>This user's visibility, or null until the server says.</summary>
    public PresenceVisibility? Visibility { get; private set; }

    /// <summary>What the user is playing, or null when they aren't playing or hide it.</summary>
    public UserPresence? Get(Guid userId) => _byUser.TryGetValue(userId, out var presence) ? presence : null;

    /// <summary>Everyone playing the game, by name.</summary>
    public IReadOnlyList<UserPresence> ForGame(Guid gameId) => _byUser.Values
        .Where(p => p.GameId == gameId)
        .OrderBy(p => p.UserName, StringComparer.CurrentCultureIgnoreCase)
        .ToList();

    /// <summary>Fetches everyone's presence and this user's visibility again.</summary>
    public async Task RefreshAsync()
    {
        var generation = Interlocked.Increment(ref _refreshGeneration);

        var all = await _client.GetAsync();
        var visibility = await _client.GetVisibilityAsync();

        // A later refresh, or a disconnect, has overtaken this one
        if (generation != Volatile.Read(ref _refreshGeneration))
            return;

        _byUser.Clear();

        foreach (var presence in all)
            _byUser[presence.UserId] = presence;

        SetVisibility(visibility);

        Changed?.Invoke(this, null);
    }

    /// <summary>Sets this user's visibility. False when the server didn't take it.</summary>
    public async Task<bool> SetVisibilityAsync(PresenceVisibility visibility)
    {
        if (!await _client.SetVisibilityAsync(visibility))
            return false;

        SetVisibility(visibility);

        return true;
    }

    private void SetVisibility(PresenceVisibility? visibility)
    {
        if (Visibility == visibility)
            return;

        Visibility = visibility;
        VisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnPresenceChanged(object? sender, UserPresence presence)
    {
        _byUser[presence.UserId] = presence;
        Changed?.Invoke(this, presence.UserId);
    }

    private void OnPresenceEnded(object? sender, Guid userId)
    {
        if (_byUser.TryRemove(userId, out _))
            Changed?.Invoke(this, userId);
    }

    private void OnConnected(object? sender, EventArgs e)
    {
        _ = RefreshSafelyAsync();
    }

    private void OnDisconnected(object? sender, EventArgs e)
    {
        Interlocked.Increment(ref _refreshGeneration);

        // Nothing pushed while disconnected would arrive, so what's known can only go stale
        _byUser.Clear();
        Changed?.Invoke(this, null);
    }

    private async Task RefreshSafelyAsync()
    {
        try
        {
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not refresh presence");
        }
    }

    public void Dispose()
    {
        _client.PresenceChanged -= OnPresenceChanged;
        _client.PresenceEnded -= OnPresenceEnded;
        _client.Connected -= OnConnected;
        _client.Disconnected -= OnDisconnected;
    }
}
