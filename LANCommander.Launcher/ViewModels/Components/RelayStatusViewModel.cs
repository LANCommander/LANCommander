using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Interposer;
using LANCommander.Launcher.Services.Interposer;

namespace LANCommander.Launcher.ViewModels.Components;

/// <summary>
/// A running game's place on the server's network relay, for its page: whether it is connected, the
/// address other players see it at, who else is in the network, and a way to leave and rejoin.
/// Follows the game's <see cref="InterposerSession"/>, which the Interposer keeps up to date.
/// </summary>
public partial class RelayStatusViewModel : ViewModelBase
{
    /// <summary>The session followed, or null when the game is not on the relay.</summary>
    public InterposerSession? Session { get; private set; }

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConnected), nameof(IsConnecting), nameof(IsFailed), nameof(IsOff),
        nameof(CanDisconnect), nameof(CanReconnect), nameof(HasNoPlayers))]
    private RelayState _state;

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private string _explanation = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDisconnect), nameof(CanReconnect))]
    private bool _isBusy;

    public ObservableCollection<RelayPlayer> Players { get; } = new();

    public bool IsConnected => State == RelayState.Connected;

    /// <summary>On the way: connecting for the first time, or again after losing the server.</summary>
    public bool IsConnecting => State is RelayState.Connecting or RelayState.Reconnecting;

    public bool IsFailed => State == RelayState.Failed;

    public bool IsOff => State == RelayState.Disabled;

    public bool HasNoPlayers => IsConnected && Players.Count == 0;

    // Shown only while following a session, so these need not ask whether there is one
    public bool CanDisconnect => !IsBusy && State is RelayState.Connected or RelayState.Connecting or RelayState.Reconnecting;

    public bool CanReconnect => !IsBusy && State is RelayState.Disabled or RelayState.Failed;

    /// <summary>Follows <paramref name="session"/>, or hides when it is null. Call on the UI thread.</summary>
    public void Follow(InterposerSession? session)
    {
        if (ReferenceEquals(session, Session))
            return;

        if (Session != null)
            Session.Changed -= OnSessionChanged;

        Session = session;

        if (session != null)
            session.Changed += OnSessionChanged;

        Apply(session?.Relay);
    }

    // The Interposer reports from its own thread
    private void OnSessionChanged(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, Session))
            Dispatcher.UIThread.Post(() => Apply(Session?.Relay));
    }

    /// <summary>Shows <paramref name="relay"/>, or hides when it is null.</summary>
    public void Apply(RelaySnapshot? relay)
    {
        IsVisible = relay != null;

        if (relay == null)
            return;

        State = relay.State;

        Players.Clear();

        foreach (var player in relay.Players)
            Players.Add(player);

        var others = relay.Players.Count == 1 ? "1 other player" : $"{relay.Players.Count} other players";

        Summary = relay.State switch
        {
            RelayState.Connecting => "Connecting",
            RelayState.Connected => relay.Players.Count == 0 ? "Connected" : $"Connected · {others}",
            RelayState.Reconnecting => "Reconnecting",
            RelayState.Failed => "Couldn't join",
            _ => "Off",
        };

        Explanation = relay.State switch
        {
            RelayState.Connecting => "Joining the server's network relay...",
            RelayState.Connected => string.IsNullOrEmpty(relay.VirtualAddress)
                ? "On the server's network relay."
                : $"On the server's network relay. Other players see you at {relay.VirtualAddress}.",
            RelayState.Reconnecting => "Lost the relay server and trying again. You keep your address.",
            RelayState.Failed => "The server would not let this game into its relay. Try reconnecting.",
            _ => "Off the relay. Players on other networks can't see you until you reconnect.",
        };

        OnPropertyChanged(nameof(HasNoPlayers));
    }

    [RelayCommand]
    private Task DisconnectAsync() => RunAsync(session => session.DisconnectRelayAsync());

    [RelayCommand]
    private Task ReconnectAsync() => RunAsync(session => session.ReconnectRelayAsync());

    private async Task RunAsync(Func<InterposerSession, Task> action)
    {
        if (Session == null || IsBusy)
            return;

        IsBusy = true;

        try
        {
            await action(Session);
        }
        catch (Exception ex) when (ex is InvalidOperationException or TimeoutException or System.IO.IOException or OperationCanceledException)
        {
            // The game may have exited, or stopped answering; the next update shows where it stands
            Explanation = "The game didn't respond. It may have closed.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
