using LANCommander.Interposer;
using LANCommander.Launcher.Services.Interposer;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace LANCommander.Launcher.Services.Tests.Interposer;

public class InterposerSessionServiceTests
{
    private static InterposerSessionService Sessions() => new(NullLogger<InterposerSessionService>.Instance);

    [Fact]
    public void AGameJoiningTheRelayStartsOutConnecting()
    {
        var sessions = Sessions();
        var gameId = Guid.NewGuid();

        using var interposer = new InterposerService();
        using var _ = sessions.Register(gameId, interposer, joinsRelay: true);

        var relay = sessions.Get(gameId).ShouldNotBeNull().Relay;

        relay.State.ShouldBe(RelayState.Connecting);
        relay.Players.ShouldBeEmpty();
    }

    [Fact]
    public void PlayersComeAndGoAndAreListedByName()
    {
        var sessions = Sessions();
        var gameId = Guid.NewGuid();

        using var interposer = new InterposerService();
        using var _ = sessions.Register(gameId, interposer, joinsRelay: true);

        var session = sessions.Get(gameId)!;
        var changes = 0;
        session.Changed += (_, _) => changes++;

        session.Apply(RelayState.Connected, "10.13.37.5");
        session.ApplyPlayer(new RelayPlayer(2, "Zoe", "10.13.37.7"), joined: true);
        session.ApplyPlayer(new RelayPlayer(3, "alex", "10.13.37.6"), joined: true);

        session.Relay.VirtualAddress.ShouldBe("10.13.37.5");
        session.Relay.Players.Select(p => p.Name).ShouldBe(["alex", "Zoe"]);

        session.ApplyPlayer(new RelayPlayer(2, "Zoe", "10.13.37.7"), joined: false);

        session.Relay.Players.ShouldHaveSingleItem().Name.ShouldBe("alex");
        changes.ShouldBe(4);
    }

    [Fact]
    public void LeavingTheRelayEmptiesTheNetworkButKeepsTheAddress()
    {
        var sessions = Sessions();
        var gameId = Guid.NewGuid();

        using var interposer = new InterposerService();
        using var _ = sessions.Register(gameId, interposer, joinsRelay: true);

        var session = sessions.Get(gameId)!;

        session.Apply(RelayState.Connected, "10.13.37.5");
        session.ApplyPlayer(new RelayPlayer(2, "Zoe", "10.13.37.7"), joined: true);

        // A state with no address of its own keeps the one it had
        session.Apply(RelayState.Disabled, null);

        session.Relay.Players.ShouldBeEmpty();
        session.Relay.VirtualAddress.ShouldBe("10.13.37.5");
    }

    [Fact]
    public void AGameThatExitsHasNoSession()
    {
        var sessions = Sessions();
        var gameId = Guid.NewGuid();

        using var interposer = new InterposerService();
        var registration = sessions.Register(gameId, interposer, joinsRelay: false);

        sessions.Get(gameId).ShouldNotBeNull().Relay.State.ShouldBe(RelayState.Disabled);

        registration.Dispose();
        registration.Dispose();

        sessions.Get(gameId).ShouldBeNull();
    }

    [Fact]
    public void AnOldSessionEndingLeavesTheGamesNewOneAlone()
    {
        var sessions = Sessions();
        var gameId = Guid.NewGuid();

        using var first = new InterposerService();
        using var second = new InterposerService();

        var old = sessions.Register(gameId, first, joinsRelay: true);
        using var current = sessions.Register(gameId, second, joinsRelay: true);

        old.Dispose();

        sessions.Get(gameId).ShouldNotBeNull();
    }
}
