using LANCommander.SDK.Models;
using LANCommander.SDK.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace LANCommander.Launcher.Services.Tests.Tests;

/// <summary>The launcher's view of who is playing, driven the way the RPC hub drives it.</summary>
public class PresenceServiceTests
{
    private readonly PresenceClient _client = new(NullLogger<PresenceClient>.Instance);
    private readonly PresenceService _presence;
    private readonly List<Guid?> _changes = new();

    public PresenceServiceTests()
    {
        _presence = new PresenceService(_client, NullLogger<PresenceService>.Instance);
        _presence.Changed += (_, userId) => _changes.Add(userId);
    }

    private static UserPresence Playing(Guid gameId, string name) => new()
    {
        UserId = Guid.NewGuid(),
        UserName = name,
        GameId = gameId,
        GameTitle = "Game",
        StartedOn = DateTime.UtcNow,
    };

    [Fact]
    public void PushedPresenceIsKeptUntilItEnds()
    {
        var game = Guid.NewGuid();
        var bob = Playing(game, "bob");
        var alice = Playing(game, "Alice");

        _client.OnPresenceChanged(bob);
        _client.OnPresenceChanged(alice);

        _presence.Get(bob.UserId).ShouldBe(bob);
        _presence.ForGame(game).Select(p => p.UserName).ShouldBe(["Alice", "bob"]);
        _presence.ForGame(Guid.NewGuid()).ShouldBeEmpty();

        _client.OnPresenceEnded(bob.UserId);

        _presence.Get(bob.UserId).ShouldBeNull();
        _changes.ShouldBe([bob.UserId, alice.UserId, bob.UserId]);
    }

    [Fact]
    public void ANewerPresenceReplacesTheOld()
    {
        var bob = Playing(Guid.NewGuid(), "bob");
        var nextGame = Playing(Guid.NewGuid(), "bob");
        nextGame.UserId = bob.UserId;

        _client.OnPresenceChanged(bob);
        _client.OnPresenceChanged(nextGame);

        _presence.Get(bob.UserId)!.GameId.ShouldBe(nextGame.GameId);
        _presence.ForGame(bob.GameId).ShouldBeEmpty();
    }

    [Fact]
    public void EndingSomeoneNotPlayingChangesNothing()
    {
        _client.OnPresenceEnded(Guid.NewGuid());

        _changes.ShouldBeEmpty();
    }

    [Fact]
    public void LosingTheConnectionForgetsEveryone()
    {
        var bob = Playing(Guid.NewGuid(), "bob");
        _client.OnPresenceChanged(bob);

        _client.OnDisconnected();

        _presence.Get(bob.UserId).ShouldBeNull();
        _changes.Last().ShouldBeNull();
    }

    [Fact]
    public async Task WithNoServerARefreshLeavesNothingAndNoVisibility()
    {
        _client.OnPresenceChanged(Playing(Guid.NewGuid(), "bob"));

        await _presence.RefreshAsync();

        _presence.Visibility.ShouldBeNull();
        _changes.Last().ShouldBeNull();
        (await _presence.SetVisibilityAsync(SDK.Enums.PresenceVisibility.Hidden)).ShouldBeFalse();
    }
}
