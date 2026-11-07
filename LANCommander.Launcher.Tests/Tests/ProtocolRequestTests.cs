using System;
using System.Collections.Generic;
using LANCommander.Launcher.Services.Protocol;
using LANCommander.SDK.Enums;
using Xunit;
using ManifestAction = LANCommander.SDK.Models.Manifest.Action;

namespace LANCommander.Launcher.Tests.Tests;

/// <summary>
/// lancommander:// links: which ones the launcher accepts, which action a play link runs, and that
/// a link's parameters can only reach the action's arguments.
/// </summary>
public class ProtocolRequestTests
{
    private const string GameId = "0b9f7d6e-1c7a-4a5e-9d0e-3f1b2c4d5e6f";
    private const string ActionId = "6a1e2c3d-4b5f-4a6e-8d7c-9e0f1a2b3c4d";

    private static ProtocolRequest Parse(string url)
    {
        Assert.True(ProtocolRequest.TryParse(url, out var request, out var error), error);
        return request!;
    }

    private static string Reject(string url)
    {
        Assert.False(ProtocolRequest.TryParse(url, out var request, out var error));
        Assert.Null(request);
        return error!;
    }

    [Theory]
    [InlineData($"lancommander://game/{GameId}")]
    [InlineData($"lancommander://game/{GameId}/")]
    [InlineData($"LANCommander://Game/{GameId}")]
    public void AGameLinkOpensTheGamesPage(string url)
    {
        var request = Parse(url);

        Assert.Equal(ProtocolRequestKind.ViewGame, request.Kind);
        Assert.Equal(Guid.Parse(GameId), request.GameId);
        Assert.Null(request.ActionId);
        Assert.Empty(request.Parameters);
    }

    [Fact]
    public void AGamePageLinkIgnoresItsQuery() =>
        Assert.Empty(Parse($"lancommander://game/{GameId}?Map=dm1").Parameters);

    [Fact]
    public void APlayLinkWithoutAnActionRunsTheDefault()
    {
        var request = Parse($"lancommander://game/{GameId}/play");

        Assert.Equal(ProtocolRequestKind.PlayGame, request.Kind);
        Assert.Null(request.ActionId);
    }

    [Fact]
    public void APlayLinkNamesItsActionAndParameters()
    {
        var request = Parse($"lancommander://game/{GameId}/PLAY?action={ActionId}&ServerHost=10.0.0.5&ServerPort=27015");

        Assert.Equal(Guid.Parse(ActionId), request.ActionId);
        Assert.Equal(new Dictionary<string, string>
        {
            ["ServerHost"] = "10.0.0.5",
            ["ServerPort"] = "27015",
        }, request.Parameters);
    }

    [Theory]
    [InlineData("Player%20One")]
    [InlineData("Player+One")]
    public void ParameterValuesAreDecoded(string encoded) =>
        Assert.Equal("Player One", Parse($"lancommander://game/{GameId}/play?Name={encoded}").Parameters["Name"]);

    [Fact]
    public void AParameterWithoutAValueIsEmpty() =>
        Assert.Equal(string.Empty, Parse($"lancommander://game/{GameId}/play?Spectate").Parameters["Spectate"]);

    [Theory]
    [InlineData("lancommander://game/not-a-guid")]
    [InlineData("lancommander://game")]
    [InlineData($"lancommander://server/{GameId}")]
    [InlineData($"lancommander://game/{GameId}/install")]
    [InlineData($"lancommander://game/{GameId}/play/extra")]
    [InlineData("https://example.com")]
    [InlineData("")]
    public void RejectsLinksItCantOpen(string url) => Reject(url);

    [Fact]
    public void RejectsAnActionThatIsntAnId() =>
        Reject($"lancommander://game/{GameId}/play?action=Multiplayer");

    [Theory]
    [InlineData("1Name")]
    [InlineData("Na-me")]
    [InlineData("%7BInstallDir%7D")]
    public void RejectsParameterNamesThatArentPlaceholders(string name) =>
        Reject($"lancommander://game/{GameId}/play?{name}=x");

    [Theory]
    [InlineData("x%22%20-exec%20evil")]
    [InlineData("x%0A-exec")]
    public void RejectsValuesThatWouldBreakOutOfAnArgument(string value) =>
        Reject($"lancommander://game/{GameId}/play?Name={value}");

    [Theory]
    [InlineData("lancommander://game/x", true)]
    [InlineData("LANCOMMANDER:anything", true)]
    [InlineData("--big-screen", false)]
    [InlineData(null, false)]
    public void RecognisesLinksMeantForTheLauncher(string? value, bool expected) =>
        Assert.Equal(expected, ProtocolRequest.IsProtocolUrl(value));

    private static readonly ManifestAction Primary = new() { Id = Guid.NewGuid(), Name = "Play", Type = ActionType.Primary };
    private static readonly ManifestAction Secondary = new() { Id = Guid.Parse(ActionId), Name = "Dedicated Server", Type = ActionType.Secondary };
    private static readonly ManifestAction Setup = new() { Id = Guid.NewGuid(), Name = "Setup", Type = ActionType.Config };
    private static readonly ManifestAction Join = new() { Id = Guid.NewGuid(), Name = "Join", Type = ActionType.Join };
    private static readonly ManifestAction JoinLan = new() { Id = Guid.NewGuid(), Name = "Join LAN", Type = ActionType.Join };

    [Fact]
    public void AJoinLinkIsParsedLikeAPlayLink()
    {
        var request = Parse($"lancommander://game/{GameId}/join?ServerHost=10.0.0.5");

        Assert.Equal(ProtocolRequestKind.JoinGame, request.Kind);
        Assert.Equal("10.0.0.5", request.Parameters["ServerHost"]);
    }

    [Fact]
    public void PlaysThePrimaryActionByDefault() =>
        Assert.Same(Primary, ProtocolActions.Select([Setup, Join, Secondary, Primary], ProtocolRequestKind.PlayGame, null));

    [Fact]
    public void WithoutAPrimaryActionPlaysTheFirstSecondaryOne() =>
        Assert.Same(Secondary, ProtocolActions.Select([Setup, Join, Secondary], ProtocolRequestKind.PlayGame, null));

    [Fact]
    public void NeverPlaysAConfigOrJoinActionByDefault() =>
        Assert.Null(ProtocolActions.Select([Setup, Join], ProtocolRequestKind.PlayGame, null));

    [Fact]
    public void APlayLinkCanNameAnActionOfAnyType()
    {
        Assert.Same(Secondary, ProtocolActions.Select([Primary, Secondary], ProtocolRequestKind.PlayGame, Guid.Parse(ActionId)));
        Assert.Same(Setup, ProtocolActions.Select([Primary, Setup], ProtocolRequestKind.PlayGame, Setup.Id));
    }

    [Fact]
    public void AnUnknownActionIdPicksNothingRatherThanTheDefault() =>
        Assert.Null(ProtocolActions.Select([Primary, Secondary], ProtocolRequestKind.PlayGame, Guid.NewGuid()));

    [Fact]
    public void JoinsTheFirstJoinAction() =>
        Assert.Same(Join, ProtocolActions.Select([Primary, Join, JoinLan], ProtocolRequestKind.JoinGame, null));

    [Fact]
    public void AJoinLinkCanNameOneOfTheJoinActions() =>
        Assert.Same(JoinLan, ProtocolActions.Select([Primary, Join, JoinLan], ProtocolRequestKind.JoinGame, JoinLan.Id));

    [Fact]
    public void AJoinLinkOnlyRunsJoinActions()
    {
        Assert.Null(ProtocolActions.Select([Primary, Secondary], ProtocolRequestKind.JoinGame, null));
        Assert.Null(ProtocolActions.Select([Primary, Join], ProtocolRequestKind.JoinGame, Primary.Id));
    }

    [Fact]
    public void ParametersFillTheArgumentsPlaceholdersOnly()
    {
        var action = new ManifestAction
        {
            Name = "Join Server",
            Path = "{InstallDir}/game.exe",
            WorkingDirectory = "{InstallDir}",
            Arguments = "+connect {ServerHost}:{ServerPort} -name \"{PlayerName}\" -dir {InstallDir}",
            Variables = new() { ["ServerPort"] = "27960" },
        };

        var applied = ProtocolActions.ApplyParameters(action, new Dictionary<string, string>
        {
            ["ServerHost"] = "10.0.0.5",
            ["InstallDir"] = "C:\\elsewhere",
        });

        Assert.Equal("+connect 10.0.0.5:{ServerPort} -name \"{PlayerName}\" -dir C:\\elsewhere", applied.Arguments);
        Assert.Equal("{InstallDir}/game.exe", applied.Path);
        Assert.Equal("{InstallDir}", applied.WorkingDirectory);
        // The link's InstallDir must not become a variable, which would change the path too
        Assert.Equal(new Dictionary<string, string> { ["ServerPort"] = "27960" }, applied.Variables);
        // The action the launcher listed is left as it was
        Assert.Contains("{ServerHost}", action.Arguments);
    }

    [Fact]
    public void ReportsParametersTheActionDoesntUse()
    {
        var action = new ManifestAction { Arguments = "+connect {ServerHost}" };

        Assert.Equal(["Map"], ProtocolActions.UnusedParameters(action, new Dictionary<string, string>
        {
            ["ServerHost"] = "10.0.0.5",
            ["Map"] = "dm1",
        }));
    }

    [Theory]
    [InlineData("/opt/LANCommander/LANCommander.Launcher", "\"/opt/LANCommander/LANCommander.Launcher\"")]
    [InlineData("/home/me/My Games/LANCommander.AppImage", "\"/home/me/My Games/LANCommander.AppImage\"")]
    [InlineData("/home/me/$HOME/100%/a\"b", "\"/home/me/\\\\$HOME/100%%/a\\\\\"b\"")]
    public void QuotesDesktopEntryExecArguments(string argument, string expected) =>
        Assert.Equal(expected, ProtocolHandlerRegistration.QuoteDesktopExecArgument(argument));
}
