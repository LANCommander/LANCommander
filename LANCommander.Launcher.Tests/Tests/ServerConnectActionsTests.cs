using System;
using System.Collections.Generic;
using LANCommander.Launcher.Services;
using LANCommander.SDK.Enums;
using Xunit;
using ManifestAction = LANCommander.SDK.Models.Manifest.Action;

namespace LANCommander.Launcher.Tests.Tests;

/// <summary>
/// What a game server's Connect button runs: the server's own primary action, else the game's Join
/// action pointed at the server.
/// </summary>
public class ServerConnectActionsTests
{
    private static readonly Guid ServerId = Guid.NewGuid();
    private static readonly Guid OtherServerId = Guid.NewGuid();

    private static ManifestAction ServerAction(Guid serverId, ActionType type, string name = "Connect") => new()
    {
        Name = name,
        Type = type,
        Arguments = "+connect {ServerHost}:{ServerPort}",
        Variables = new() { ["ServerId"] = serverId.ToString(), ["ServerHost"] = "10.0.0.5" },
    };

    private static readonly ManifestAction Play = new() { Name = "Play", Type = ActionType.Primary };

    private static readonly ManifestAction GameJoin = new()
    {
        Name = "Join",
        Type = ActionType.Join,
        Arguments = "+connect {ServerHost}:{ServerPort}",
        Variables = new() { ["Mode"] = "coop" },
    };

    [Fact]
    public void RunsTheServersPrimaryAction()
    {
        var own = ServerAction(ServerId, ActionType.Primary);

        Assert.Same(own, ServerConnectActions.Resolve(ServerId, "10.0.0.5", 27015, [Play, GameJoin, own]));
    }

    [Fact]
    public void IgnoresAnotherServersPrimaryAction()
    {
        var other = ServerAction(OtherServerId, ActionType.Primary);

        var resolved = ServerConnectActions.Resolve(ServerId, "10.0.0.5", 27015, [other, GameJoin]);

        Assert.Equal("Join", resolved?.Name);
    }

    [Fact]
    public void WithoutAPrimaryServerActionFallsBackToTheGamesJoinActionPointedAtTheServer()
    {
        var secondary = ServerAction(ServerId, ActionType.Secondary, "Spectate");

        var resolved = ServerConnectActions.Resolve(ServerId, "192.168.1.20", 7777, [Play, secondary, GameJoin]);

        Assert.NotNull(resolved);
        Assert.Equal("Join", resolved!.Name);
        Assert.Equal(new Dictionary<string, string>
        {
            ["Mode"] = "coop",
            ["ServerHost"] = "192.168.1.20",
            ["ServerPort"] = "7777",
            ["ServerId"] = ServerId.ToString(),
        }, resolved.Variables);

        // The game's own Join action is a template other servers use too
        Assert.Equal(new Dictionary<string, string> { ["Mode"] = "coop" }, GameJoin.Variables);
    }

    [Fact]
    public void LeavesOutAnAddressTheServerDoesntHave()
    {
        var resolved = ServerConnectActions.Resolve(ServerId, "", 0, [GameJoin]);

        Assert.False(resolved!.Variables.ContainsKey("ServerHost"));
        Assert.False(resolved.Variables.ContainsKey("ServerPort"));
    }

    [Fact]
    public void AnotherServersJoinActionIsNotTheGamesTemplate() =>
        Assert.Null(ServerConnectActions.Resolve(ServerId, "10.0.0.5", 27015, [Play, ServerAction(OtherServerId, ActionType.Join)]));

    [Fact]
    public void WithNeitherThereIsNothingToConnectWith() =>
        Assert.Null(ServerConnectActions.Resolve(ServerId, "10.0.0.5", 27015, [Play]));

    [Fact]
    public void AServersPrimaryActionIsOfferedLikeAnyOther()
    {
        var own = ServerAction(ServerId, ActionType.Primary);

        Assert.Same(own, ActionTypes.SelectDefault([own, Play]));
    }
}
