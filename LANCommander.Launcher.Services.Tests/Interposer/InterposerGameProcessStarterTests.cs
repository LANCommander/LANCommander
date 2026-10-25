using System.Diagnostics;
using System.Net;
using LANCommander.Interposer.Relay;
using LANCommander.Launcher.Services.Interposer;
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

namespace LANCommander.Launcher.Services.Tests.Interposer;

public class InterposerGameProcessStarterTests : IDisposable
{
    private readonly DirectoryInfo _installDirectory = Directory.CreateTempSubdirectory("interposer-starter");

    public void Dispose()
    {
        // A game the test killed may still be letting go of its log
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                _installDirectory.Delete(recursive: true);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(200);
            }
        }
    }

    private static Game LanGame() => new()
    {
        Id = Guid.NewGuid(),
        Title = "Quake III Arena",
        MultiplayerModes = [new MultiplayerMode { Type = MultiplayerType.LAN, NetworkProtocol = NetworkProtocol.TCPIP }],
    };

    private static Game SinglePlayerGame() => new() { Id = Guid.NewGuid(), Title = "Single Player Game" };

    [Fact]
    public async Task ItDeclinesWhenThePlayerHasTurnedTheRelayOff()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var relay = new FakeRelayClient(Session(41300));
        var starter = Starter(relay, relayEnabled: false);

        (await starter.StartAsync(Context(LanGame(), PingExe32()), CancellationToken.None)).ShouldBeNull();
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

        (await Starter(new FakeRelayClient(null)).StartAsync(Context(LanGame(), PingExe32()), CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public async Task APlayerWhoTurnsTheRelayOffForAGameKeepsItOutOfTheRelay()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var game = LanGame();
        PlayerValues(game, ("NetworkRelay.Enabled", "false"));

        var relay = new FakeRelayClient(Session(41300));

        (await Starter(relay).StartAsync(Context(game, PingExe32()), CancellationToken.None)).ShouldBeNull();
        relay.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task APlayerCanPutAGameWithNoLanModeInTheRelay()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var game = SinglePlayerGame();
        PlayerValues(game, ("NetworkRelay.Enabled", "true"));

        // The server has no relay to give, so it asks for a session, gets none, and -- rather than leave
        // the game waiting on a relay that will never answer -- starts it normally
        var relay = new FakeRelayClient(null);

        (await Starter(relay, relayEnabled: false).StartAsync(Context(game, PingExe32()), CancellationToken.None)).ShouldBeNull();

        relay.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task AGameWithNothingToApplyStartsWithoutTheInterposer()
    {
        if (!OperatingSystem.IsWindows())
            return;

        (await Starter(new FakeRelayClient(Session(41300))).StartAsync(Context(SinglePlayerGame(), PingExe32()), CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public async Task TheLaunchersCopyIsNeverInjectedWhenThePlayerHasTurnedItOff()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var game = SinglePlayerGame();
        PlayerValues(game, ("OsVersion.Version", "WindowsXP"));

        (await Starter(new FakeRelayClient(null), interposerEnabled: false).StartAsync(Context(game, PingExe32()), CancellationToken.None)).ShouldBeNull();
    }

    /// <summary>
    /// The whole way through for the launcher's own copy: a player's setting alone starts the game
    /// under the Interposer, whose config, logs and registry go to the game's install, with the
    /// player's value in its config.
    /// </summary>
    [Fact]
    public async Task APlayersSettingStartsTheGameUnderTheLaunchersCopyWithItsOwnBaseDirectory()
    {
        if (!OperatingSystem.IsWindows() || !Environment.Is64BitOperatingSystem)
            return;

        var game = SinglePlayerGame();
        PlayerValues(game, ("OsVersion.Version", "WindowsXP"));

        var context = Context(game, PingExe32());

        using var started = await Starter(new FakeRelayClient(null)).StartAsync(context, CancellationToken.None);

        started.ShouldNotBeNull();

        try
        {
            var logs = Path.Combine(InterposerGameProcessStarter.GetBaseDirectory(context), ".interposer", "Logs");
            var deadline = DateTime.UtcNow.AddSeconds(15);
            string? log = null;

            while (DateTime.UtcNow < deadline)
            {
                log = Directory.Exists(logs) ? Directory.GetFiles(logs, "*.log").Select(ReadShared).FirstOrDefault(l => l.Contains("overlay")) : null;

                if (log != null)
                    break;

                await Task.Delay(100);
            }

            log.ShouldNotBeNull();
            log.ShouldContain("applied the launcher's config overlay");
        }
        finally
        {
            started.Process.Kill();
            await started.Process.WaitForExitAsync();
        }
    }

    /// <summary>
    /// A game whose action runs the redistributable's injector is started by the launcher itself, with
    /// the DLL beside the injector -- so the player's settings reach the game, not the injector, which
    /// would otherwise be the process they were set up for. Its config stays beside that DLL.
    /// </summary>
    [Fact]
    public async Task AnInjectorActionStartsTheGameItLaunchesWithTheDllBesideTheInjector()
    {
        if (!OperatingSystem.IsWindows() || !Environment.Is64BitOperatingSystem)
            return;

        // What the redistributable's Injector method installs, for a 32-bit game
        var bin = Directory.CreateDirectory(Path.Combine(_installDirectory.FullName, "Bin")).FullName;
        var packaged = Path.Combine(AppContext.BaseDirectory, "x86");

        File.Copy(Path.Combine(packaged, "LANCommander.Interposer.dll"), Path.Combine(bin, "LANCommander.Interposer.dll"));
        File.Copy(Path.Combine(packaged, InjectorCommandLine.InjectorFileName), Path.Combine(bin, InjectorCommandLine.InjectorFileName));
        File.Copy(PingExe32().FileName, Path.Combine(bin, "ping.exe"));

        var game = SinglePlayerGame();
        PlayerValues(game, ("OsVersion.Version", "WindowsXP"));

        var action = new ProcessStartInfo(Path.Combine(bin, InjectorCommandLine.InjectorFileName), "--launch ping.exe -n 30 127.0.0.1")
        {
            WorkingDirectory = bin,
            UseShellExecute = true,
        };

        using var started = await Starter(new FakeRelayClient(null)).StartAsync(Context(game, action), CancellationToken.None);

        started.ShouldNotBeNull();

        try
        {
            started.Process.MainModule!.FileName.ShouldBe(Path.Combine(bin, "ping.exe"), StringCompareShould.IgnoreCase);

            var logs = Path.Combine(bin, ".interposer", "Logs");
            var deadline = DateTime.UtcNow.AddSeconds(15);
            string? log = null;

            while (DateTime.UtcNow < deadline)
            {
                log = Directory.Exists(logs) ? Directory.GetFiles(logs, "*.log").Select(ReadShared).FirstOrDefault(l => l.Contains("overlay")) : null;

                if (log != null)
                    break;

                await Task.Delay(100);
            }

            log.ShouldNotBeNull();
        }
        finally
        {
            started.Process.Kill();
            await started.Process.WaitForExitAsync();
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
        var sessions = new InterposerSessionService(NullLogger<InterposerSessionService>.Instance);
        var game = LanGame();

        var started = await Starter(relay, sessions: sessions).StartAsync(Context(game, PingExe32()), CancellationToken.None);

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

            // The launcher sees the game on the relay, under the address the server gave it
            var session = sessions.Get(game.Id).ShouldNotBeNull();
            session.JoinsRelay.ShouldBeTrue();

            await Until(() => session.Relay.State == LANCommander.Interposer.RelayState.Connected);
            session.Relay.VirtualAddress.ShouldNotBeNullOrEmpty();

            // And can take it off and put it back, with a fresh session
            await session.DisconnectRelayAsync();
            await Until(() => server.GetClients().Count == 0);
            session.Relay.State.ShouldBe(LANCommander.Interposer.RelayState.Disabled);

            (await session.ReconnectRelayAsync()).ShouldBeTrue();
            await Until(() => server.GetClients().Count == 1 && session.Relay.State == LANCommander.Interposer.RelayState.Connected);
            relay.Calls.ShouldBe(2);
        }
        finally
        {
            started.Process.Kill();
            await started.Process.WaitForExitAsync();
            started.Dispose();
        }

        // Its session ends with the game
        sessions.Get(game.Id).ShouldBeNull();
    }

    private static async Task Until(Func<bool> condition, int seconds = 15)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);

        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(50);

        condition().ShouldBeTrue();
    }

    [Fact]
    public void OnlyPlainExecutablesAreStartedUnderTheInterposer()
    {
        var directory = _installDirectory.FullName;

        var exe = Path.Combine(directory, "game.exe");
        var batch = Path.Combine(directory, "game.bat");
        File.WriteAllText(exe, "");
        File.WriteAllText(batch, "");

        InterposerGameProcessStarter.ResolveExecutable(new ProcessStartInfo(exe)).ShouldBe(exe);
        InterposerGameProcessStarter.ResolveExecutable(new ProcessStartInfo("game.exe") { WorkingDirectory = directory }).ShouldBe(exe);

        InterposerGameProcessStarter.ResolveExecutable(new ProcessStartInfo(exe) { Verb = "runas" }).ShouldBeNull();
        InterposerGameProcessStarter.ResolveExecutable(new ProcessStartInfo(batch)).ShouldBeNull();
        InterposerGameProcessStarter.ResolveExecutable(new ProcessStartInfo(Path.Combine(directory, "missing.exe"))).ShouldBeNull();
        InterposerGameProcessStarter.ResolveExecutable(new ProcessStartInfo("steam://rungameid/2200")).ShouldBeNull();
    }

    [Fact]
    public void RichPresenceGetsTheGamesTitleAndApplicationWhereNothingElseSetsThem()
    {
        var game = new Game
        {
            Title = "Unreal Tournament",
            ExternalIds = [new SDK.Models.Manifest.GameExternalId { Provider = "Discord", ExternalId = "424242" }],
        };

        var options = new InterposerOptions(null, null, new Dictionary<string, string> { ["RichPresence.Discord.Enabled"] = "true" }, false);
        var overlay = options.GetOverlayValues(bundled: true);

        InterposerGameProcessStarter.AddRichPresenceDefaults(overlay, options, game);

        overlay["RichPresence.Discord.ApplicationId"].ShouldBe("424242");
        overlay["RichPresence.Details"].ShouldBe("Unreal Tournament");
        overlay["RichPresence.LargeImage"].ShouldBe("logo");
    }

    [Fact]
    public void RichPresenceKeepsWhatThePlayerOrServerSet()
    {
        var game = new Game { Title = "Unreal Tournament" };

        var options = new InterposerOptions(
            new Dictionary<string, string> { ["RichPresence.Details"] = "From the server" },
            null,
            new Dictionary<string, string> { ["RichPresence.Discord.Enabled"] = "true" },
            true);

        var overlay = options.GetOverlayValues(bundled: false);

        InterposerGameProcessStarter.AddRichPresenceDefaults(overlay, options, game);

        overlay.ShouldNotContainKey("RichPresence.Details");
        overlay["RichPresence.Discord.ApplicationId"].ShouldBe(SDK.Clients.DiscordClient.DefaultApplicationId);

        // LANCommander's application has no art to show
        overlay.ShouldNotContainKey("RichPresence.LargeImage");
    }

    [Fact]
    public void RichPresenceThatIsOffAddsNothing()
    {
        var options = new InterposerOptions(null, null, null, false);
        var overlay = options.GetOverlayValues(bundled: true);

        InterposerGameProcessStarter.AddRichPresenceDefaults(overlay, options, new Game { Title = "Anything" });

        overlay.ShouldBeEmpty();
    }

    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static void PlayerValues(Game game, params (string Key, string Value)[] values) =>
        game.RedistributableOptions[InterposerOptions.RedistributableId] = values.ToDictionary(v => v.Key, v => v.Value);

    private static RelaySession Session(int port) => new()
    {
        Host = "127.0.0.1",
        Port = port,
        Network = "game-network",
        Ticket = "ticket",
        ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
    };

    private GameProcessStartContext Context(Game game, ProcessStartInfo startInfo) =>
        new(startInfo, game.Id, _installDirectory.FullName, game, new SDK.Models.Manifest.Action { Name = "Play", IsPrimaryAction = true });

    // Long enough to be joined; killed when the test is done
    private static ProcessStartInfo PingExe32() =>
        new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86), "ping.exe"), "-n 30 127.0.0.1")
        {
            UseShellExecute = true,
        };

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static InterposerGameProcessStarter Starter(RelayClient relay, bool relayEnabled = true, bool interposerEnabled = true, InterposerSessionService? sessions = null)
    {
        var settings = new Mock<ISettingsProvider>();
        settings.SetupGet(provider => provider.CurrentValue).Returns(new SdkSettings
        {
            NetworkRelay = new NetworkRelaySettings { Enabled = relayEnabled },
            Interposer = new InterposerSettings { Enabled = interposerEnabled },
        });

        var connection = new Mock<IConnectionClient>();
        connection.Setup(client => client.GetServerAddress()).Returns(new Uri("http://127.0.0.1:1337"));

        // Never reaches a server: the alias lookup fails, and the game sees the Windows account name
        var profile = new ProfileClient(null!, NullLogger<ProfileClient>.Instance, connection.Object);

        return new InterposerGameProcessStarter(relay, settings.Object, connection.Object, profile,
            sessions ?? new InterposerSessionService(NullLogger<InterposerSessionService>.Instance),
            NullLogger<InterposerGameProcessStarter>.Instance);
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
