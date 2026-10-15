using LANCommander.SDK.Clients;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Plugins;
using LANCommander.SDK.Services;
using ManifestAction = LANCommander.SDK.Models.Manifest.Action;
using ManifestGame = LANCommander.SDK.Models.Manifest.Game;
using MultiplayerMode = LANCommander.SDK.Models.Manifest.MultiplayerMode;

namespace LANCommander.SDK.Tests.Plugins;

/// <summary>
/// The lobbies plugin <see cref="ILobbyProvider"/>s report become join actions on an installed
/// game, read offline from its manifest.
/// </summary>
public class LobbyProviderTests : IDisposable
{
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly string _installDirectory = Path.Combine(Path.GetTempPath(), $"lc-lobby-tests-{Guid.NewGuid()}");

    public void Dispose()
    {
        if (Directory.Exists(_installDirectory))
            Directory.Delete(_installDirectory, true);
    }

    [Fact]
    public async Task EachLobbyBecomesAnActionThatJoinsItThroughThePrimaryAction()
    {
        Install(NetworkProtocol.Lobby);

        var actions = (await Client(new FixedProvider(
            new GameLobby("1", "Alice", "+connect_lobby 1"),
            new GameLobby("2", "Bob", "+connect_lobby 2"))).GetActionsAsync(_installDirectory, _gameId)).ToList();

        Assert.Equal(["Play", "Join Alice's lobby", "Join Bob's lobby"], actions.Select(a => a.Name));

        var join = actions[1];

        Assert.Equal("-windowed +connect_lobby 1", join.Arguments);
        Assert.Equal("game.exe", join.Path);
        Assert.True(join.IsPrimaryAction);
    }

    [Fact]
    public async Task ProvidersAreNotAskedForGamesWithoutLobbies()
    {
        Install(NetworkProtocol.TCPIP);

        var provider = new FixedProvider(new GameLobby("1", "Alice", "+connect_lobby 1"));
        var actions = await Client(provider).GetActionsAsync(_installDirectory, _gameId);

        Assert.Equal(["Play"], actions.Select(a => a.Name));
        Assert.Equal(0, provider.Queries);
    }

    [Fact]
    public async Task AProviderThatFailsOrHangsDoesNotHideTheOthers()
    {
        Install(NetworkProtocol.Lobby);

        var actions = await Client(
                new ThrowingProvider(),
                new HangingProvider(),
                new FixedProvider(new GameLobby("1", "Alice", "+connect_lobby 1")))
            .GetActionsAsync(_installDirectory, _gameId);

        Assert.Equal(["Play", "Join Alice's lobby"], actions.Select(a => a.Name));
    }

    [Fact]
    public async Task ProvidersAreToldWhichGameAndWhereItIsInstalled()
    {
        Install(NetworkProtocol.Lobby);

        var provider = new FixedProvider();

        await Client(provider).GetActionsAsync(_installDirectory, _gameId);

        Assert.NotNull(provider.LastQuery);
        Assert.Equal(_gameId, provider.LastQuery.GameId);
        Assert.Equal(_installDirectory, provider.LastQuery.InstallDirectory);
        Assert.Equal(_gameId, Assert.Single(provider.LastQuery.Manifests).Id);
    }

    private void Install(NetworkProtocol protocol)
    {
        Directory.CreateDirectory(_installDirectory);

        ManifestHelper.Write(
            new ManifestGame
            {
                Id = _gameId,
                Title = "Lobby Game",
                Type = GameType.MainGame,
                Actions = [new ManifestAction { Name = "Play", Path = "game.exe", Arguments = "-windowed", IsPrimaryAction = true }],
                MultiplayerModes = [new MultiplayerMode { Type = MultiplayerType.Online, NetworkProtocol = protocol }],
            },
            _installDirectory);
    }

    // Only the manifest path is exercised; the server-facing dependencies are left null.
    private static GameClient Client(params ILobbyProvider[] providers) =>
        new(null!, null!, null!, null!, null!, new OfflineConnection(), null!, null!, null!, null!, null!, null!, providers)
        {
            LobbyProviderTimeout = TimeSpan.FromMilliseconds(200),
        };

    private sealed class FixedProvider(params GameLobby[] lobbies) : ILobbyProvider
    {
        public int Queries { get; private set; }
        public LobbyQuery? LastQuery { get; private set; }

        public Task<IEnumerable<GameLobby>> GetLobbiesAsync(LobbyQuery query, CancellationToken cancellationToken)
        {
            Queries++;
            LastQuery = query;

            return Task.FromResult<IEnumerable<GameLobby>>(lobbies);
        }
    }

    private sealed class ThrowingProvider : ILobbyProvider
    {
        public Task<IEnumerable<GameLobby>> GetLobbiesAsync(LobbyQuery query, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Platform unavailable");
    }

    /// <summary>Ignores the token, as a careless provider might.</summary>
    private sealed class HangingProvider : ILobbyProvider
    {
        public async Task<IEnumerable<GameLobby>> GetLobbiesAsync(LobbyQuery query, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, CancellationToken.None);

            return [];
        }
    }

    private sealed class OfflineConnection : IConnectionClient
    {
        public event EventHandler? OnConnect { add { } remove { } }
        public event EventHandler? OnDisconnect { add { } remove { } }
        public event EventHandler? OnServerAddressChanged { add { } remove { } }
        public event EventHandler? OnOfflineModeEnabled { add { } remove { } }

        public bool IsConnected() => false;
        public bool IsConfigured() => false;
        public bool IsOfflineMode() => true;
        public bool HasServerAddress() => false;
        public Uri GetServerAddress() => null!;
        public Task UpdateServerAddressAsync(string address) => Task.CompletedTask;
        public Task UpdateServerAddressAsync(Uri address) => Task.CompletedTask;
        public Task<bool> ConnectAsync() => Task.FromResult(false);
        public Task<bool> DisconnectAsync() => Task.FromResult(false);
        public Task EnableOfflineModeAsync() => Task.CompletedTask;
        public Task<bool> PingAsync(Uri? serverAddress = null) => Task.FromResult(false);
    }
}
