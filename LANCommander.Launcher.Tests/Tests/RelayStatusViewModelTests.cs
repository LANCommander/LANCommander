using System;
using LANCommander.Interposer;
using LANCommander.Launcher.Services.Interposer;
using LANCommander.Launcher.ViewModels.Components;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LANCommander.Launcher.Tests.Tests;

public class RelayStatusViewModelTests
{
    private static RelaySnapshot Snapshot(RelayState state, params RelayPlayer[] players) => new(state, "10.13.37.5", players);

    [Fact]
    public void NothingShowsForAGameThatIsNotOnTheRelay()
    {
        var relay = new RelayStatusViewModel();

        relay.Apply(null);

        Assert.False(relay.IsVisible);
    }

    [Fact]
    public void ConnectedShowsTheOtherPlayersAndTheAddressOthersSee()
    {
        var relay = new RelayStatusViewModel();

        relay.Apply(Snapshot(RelayState.Connected, new RelayPlayer(2, "Doomguy", "10.13.37.6"), new RelayPlayer(3, "Ranger", "10.13.37.7")));

        Assert.True(relay.IsVisible);
        Assert.True(relay.IsConnected);
        Assert.Equal("Connected · 2 other players", relay.Summary);
        Assert.Contains("10.13.37.5", relay.Explanation);
        Assert.Equal(2, relay.Players.Count);
        Assert.False(relay.HasNoPlayers);
        Assert.True(relay.CanDisconnect);
        Assert.False(relay.CanReconnect);
    }

    [Fact]
    public void ConnectedAloneSaysSo()
    {
        var relay = new RelayStatusViewModel();

        relay.Apply(Snapshot(RelayState.Connected));

        Assert.Equal("Connected", relay.Summary);
        Assert.True(relay.HasNoPlayers);
    }

    [Theory]
    [InlineData(RelayState.Connecting, "Connecting", true, false)]
    [InlineData(RelayState.Reconnecting, "Reconnecting", true, false)]
    [InlineData(RelayState.Failed, "Couldn't join", false, true)]
    [InlineData(RelayState.Disabled, "Off", false, true)]
    public void EachStateOffersWhatCanBeDoneFromIt(RelayState state, string summary, bool canDisconnect, bool canReconnect)
    {
        var relay = new RelayStatusViewModel();

        relay.Apply(Snapshot(state));

        Assert.Equal(summary, relay.Summary);
        Assert.Equal(canDisconnect, relay.CanDisconnect);
        Assert.Equal(canReconnect, relay.CanReconnect);
        Assert.False(relay.HasNoPlayers);
    }

    [Fact]
    public void FollowingASessionShowsItAndLettingGoHidesIt()
    {
        var sessions = new InterposerSessionService(NullLogger<InterposerSessionService>.Instance);
        var gameId = Guid.NewGuid();

        using var interposer = new InterposerService();
        using var registration = sessions.Register(gameId, interposer, joinsRelay: true);

        var relay = new RelayStatusViewModel();

        relay.Follow(sessions.Get(gameId));

        Assert.True(relay.IsVisible);
        Assert.Equal("Connecting", relay.Summary);
        Assert.Same(sessions.Get(gameId), relay.Session);

        relay.Follow(null);

        Assert.False(relay.IsVisible);
        Assert.Null(relay.Session);
    }
}
