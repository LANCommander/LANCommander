using System.Diagnostics;
using System.Net;
using LANCommander.Interposer.Relay;
using LANCommander.Launcher.Services.Relay;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;
using LANCommander.SDK.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using Xunit;
using Game = LANCommander.SDK.Models.Manifest.Game;
using MultiplayerMode = LANCommander.SDK.Models.Manifest.MultiplayerMode;
using SdkSettings = LANCommander.SDK.Models.Settings;

namespace LANCommander.Launcher.Services.Tests.Relay;

public class RelayGameProcessStarterTests
{
    private static readonly Game LanGame = new()
    {
        Id = Guid.NewGuid(),
        Title = "Quake III Arena",
        MultiplayerModes = [new MultiplayerMode { Type = MultiplayerType.LAN, NetworkProtocol = NetworkProtocol.TCPIP }],
    };

    [Fact]
    public async Task ItDeclinesWhenThePlayerHasTurnedTheRelayOff()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var relay = new FakeRelayClient(Session(41300));
        var starter = Starter(relay, enabled: false);

        (await starter.StartAsync(Context(LanGame, PingExe32()), CancellationToken.None)).ShouldBeNull();
        relay.Calls.ShouldBe(0);
    }

    [Theory]
    [InlineData(MultiplayerType.Online, NetworkProtocol.TCPIP)]
    [InlineData(MultiplayerType.Local, NetworkProtocol.TCPIP)]
    [InlineData(MultiplayerType.LAN, NetworkProtocol.Lobby)]
    [InlineData(MultiplayerType.LAN, NetworkProtocol.Modem)]
    public async Task ItDeclinesGamesTheRelayCannotCarry(MultiplayerType type, NetworkProtocol protocol)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var game = new Game { Id = Guid.NewGuid(), MultiplayerModes = [new MultiplayerMode { Type = type, NetworkProtocol = protocol }] };
        var relay = new FakeRelayClient(Session(41300));

        (await Starter(relay).StartAsync(Context(game, PingExe32()), CancellationToken.None)).ShouldBeNull();
        relay.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task ItDeclinesWhenTheServerHasNoRelay()
    {
        if (!OperatingSystem.IsWindows())
            return;

        (await Starter(new FakeRelayClient(null)).StartAsync(Context(LanGame, PingExe32()), CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public void OnlyPlainExecutablesAreStartedUnderTheInterposer()
    {
        var directory = Directory.CreateTempSubdirectory("relay-starter");

        try
        {
            var exe = Path.Combine(directory.FullName, "game.exe");
            var batch = Path.Combine(directory.FullName, "game.bat");
            File.WriteAllText(exe, "");
            File.WriteAllText(batch, "");

            RelayGameProcessStarter.ResolveExecutable(new ProcessStartInfo(exe)).ShouldBe(exe);
            RelayGameProcessStarter.ResolveExecutable(new ProcessStartInfo("game.exe") { WorkingDirectory = directory.FullName }).ShouldBe(exe);

            RelayGameProcessStarter.ResolveExecutable(new ProcessStartInfo(exe) { Verb = "runas" }).ShouldBeNull();
            RelayGameProcessStarter.ResolveExecutable(new ProcessStartInfo(batch)).ShouldBeNull();
            RelayGameProcessStarter.ResolveExecutable(new ProcessStartInfo(Path.Combine(directory.FullName, "missing.exe"))).ShouldBeNull();
            RelayGameProcessStarter.ResolveExecutable(new ProcessStartInfo("steam://rungameid/2200")).ShouldBeNull();
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// The whole way through: a 32-bit game, started by this 64-bit test through the Interposer's
    /// injector, joins a real relay server in the network the session named.
    /// </summary>
    [Fact]
    public async Task A32BitGameJoinsTheRelayNetworkItsSessionNames()
    {
        if (!OperatingSystem.IsWindows() || !Environment.Is64BitOperatingSystem)
            return;

        await using var server = new RelayServer(new RelayServerOptions { BindAddress = IPAddress.Loopback, Port = 0 });
        await server.StartAsync();

        var relay = new FakeRelayClient(Session(server.TcpEndPoint!.Port));

        using var started = await Starter(relay).StartAsync(Context(LanGame, PingExe32()), CancellationToken.None);

        started.ShouldNotBeNull();
        relay.Calls.ShouldBe(1);
        relay.LastTitle.ShouldBe("Quake III Arena");

        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(15);

            while (server.GetClients().Count == 0 && DateTime.UtcNow < deadline)
                await Task.Delay(50);

            var client = server.GetClients().ShouldHaveSingleItem();
            client.Network.ShouldBe("game-network");
        }
        finally
        {
            started.Process.Kill();
        }
    }

    private static RelaySession Session(int port) => new()
    {
        Host = "127.0.0.1",
        Port = port,
        Network = "game-network",
        Ticket = "ticket",
        ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
    };

    private static GameProcessStartContext Context(Game game, ProcessStartInfo startInfo) =>
        new(startInfo, game.Id, Path.GetTempPath(), game, new SDK.Models.Manifest.Action { Name = "Play", IsPrimaryAction = true });

    // Long enough to be joined; killed when the test is done
    private static ProcessStartInfo PingExe32() =>
        new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86), "ping.exe"), "-n 30 127.0.0.1")
        {
            UseShellExecute = true,
        };

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static RelayGameProcessStarter Starter(RelayClient relay, bool enabled = true)
    {
        var settings = new Mock<ISettingsProvider>();
        settings.SetupGet(provider => provider.CurrentValue).Returns(new SdkSettings { NetworkRelay = new NetworkRelaySettings { Enabled = enabled } });

        var connection = new Mock<IConnectionClient>();
        connection.Setup(client => client.GetServerAddress()).Returns(new Uri("http://127.0.0.1:1337"));

        // Never reaches a server: the alias lookup fails, and the relay shows the Windows account name
        var profile = new ProfileClient(null!, NullLogger<ProfileClient>.Instance, connection.Object);

        return new RelayGameProcessStarter(relay, settings.Object, connection.Object, profile, NullLogger<RelayGameProcessStarter>.Instance);
    }

    private sealed class FakeRelayClient(RelaySession? session) : RelayClient(null!, null!, NullLogger<RelayClient>.Instance)
    {
        public int Calls { get; private set; }

        public string? LastTitle { get; private set; }

        public override Task<RelaySession> CreateSessionAsync(Guid gameId, string? gameTitle = null, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastTitle = gameTitle;

            return Task.FromResult(session!);
        }
    }
}
