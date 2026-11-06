using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using LANCommander.Launcher.Services;
using LANCommander.SDK.Services;
using LANCommander.SDK.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Launcher.ViewModels.Components;

/// <summary>
/// The views' side of <see cref="PresenceService"/>: one <see cref="PlayerPresenceViewModel"/> per
/// player, and a live list per game, all changed on the UI thread. Every name shown in chat or a
/// game's details reads from here, so a change lands everywhere at once.
/// </summary>
public sealed partial class PresenceDirectory
{
    private static PresenceDirectory? _current;
    private static IServiceProvider? _currentServices;

    private readonly PresenceService _presence;
    private readonly IConnectionClient? _connection;
    private readonly Dictionary<Guid, PlayerPresenceViewModel> _players = new();
    private readonly List<GameList> _games = new();
    private readonly DispatcherTimer _timer;

    private PresenceDirectory(PresenceService presence, IConnectionClient? connection)
    {
        _presence = presence;
        _connection = connection;

        _presence.Changed += (_, userId) => Dispatcher.UIThread.Post(() => Apply(userId));

        _timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => Tick());
        _timer.Start();
    }

    /// <summary>
    /// The launcher's directory, or null before the app's services exist (design time). A new set of
    /// services, as each visual fixture installs, gets a directory of its own.
    /// </summary>
    public static PresenceDirectory? Current
    {
        get
        {
            var services = App.Services;

            if (!ReferenceEquals(services, _currentServices))
            {
                _current?._timer.Stop();
                _current = services?.GetService<PresenceService>() is { } presence
                    ? new PresenceDirectory(presence, services.GetService<IConnectionClient>())
                    : null;
                _currentServices = services;
            }

            return _current;
        }
    }

    /// <summary>The player's presence. The same instance every time, so bindings to it stay live.</summary>
    public PlayerPresenceViewModel For(Guid userId)
    {
        if (!_players.TryGetValue(userId, out var player))
        {
            player = new PlayerPresenceViewModel(userId);
            _players[userId] = player;
            Update(player);
        }

        return player;
    }

    /// <summary>
    /// Everyone playing the game, but <paramref name="exceptUserId"/>, kept current for as long as
    /// the caller holds it.
    /// </summary>
    public ObservableCollection<PlayerPresenceViewModel> ForGame(Guid gameId, Guid exceptUserId)
    {
        var list = new ObservableCollection<PlayerPresenceViewModel>();
        var registration = new GameList(gameId, exceptUserId, new WeakReference<ObservableCollection<PlayerPresenceViewModel>>(list));

        _games.Add(registration);

        Sync(registration, list);

        return list;
    }

    /// <summary>A list handed out by <see cref="ForGame"/>, held weakly: it goes when its view model does.</summary>
    private sealed record GameList(Guid GameId, Guid ExceptUserId, WeakReference<ObservableCollection<PlayerPresenceViewModel>> List);

    private void Apply(Guid? userId)
    {
        if (userId is { } id)
        {
            if (_players.TryGetValue(id, out var player))
                Update(player);
        }
        else
        {
            foreach (var player in _players.Values)
                Update(player);
        }

        foreach (var registration in _games.ToList())
        {
            if (registration.List.TryGetTarget(out var list))
                Sync(registration, list);
            else
                _games.Remove(registration);
        }
    }

    private void Update(PlayerPresenceViewModel player)
    {
        var presence = _presence.Get(player.UserId);

        player.Presence = presence;
        player.IconUrl = presence?.IconMediaId is { } iconId
            ? _connection?.GetServerAddress()?.Join($"/api/Media/{iconId}/Thumbnail").ToString()
            : null;
    }

    private void Sync(GameList registration, ObservableCollection<PlayerPresenceViewModel> list)
    {
        var wanted = _presence.ForGame(registration.GameId)
            .Where(p => p.UserId != registration.ExceptUserId)
            .Select(p => For(p.UserId))
            .ToList();

        for (var i = list.Count - 1; i >= 0; i--)
        {
            if (!wanted.Contains(list[i]))
                list.RemoveAt(i);
        }

        for (var i = 0; i < wanted.Count; i++)
        {
            var index = list.IndexOf(wanted[i]);

            if (index < 0)
                list.Insert(i, wanted[i]);
            else if (index != i)
                list.Move(index, i);
        }
    }

    private void Tick()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var player in _players.Values)
        {
            if (player.IsPlaying)
                player.Tick(now);
        }
    }
}
