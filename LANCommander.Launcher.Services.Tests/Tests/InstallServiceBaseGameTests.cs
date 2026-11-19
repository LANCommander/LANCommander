using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Text;
using System.Text.Json;
using LANCommander.Launcher.Data;
using LANCommander.Launcher.Models;
using LANCommander.Launcher.Services.Tests.Helpers;
using LANCommander.SDK;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Factories;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;
using LANCommander.SDK.Plugins;
using LANCommander.SDK.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using Xunit;

namespace LANCommander.Launcher.Services.Tests.Tests;

/// <summary>
/// Adding a mod whose base game isn't installed has to queue the base game first, under its own id. The queue
/// lives for the whole session while uninstalls run in their own scopes, so it reads through a fresh scope per
/// operation rather than a context that remembers the base game as installed.
/// </summary>
public sealed class InstallServiceBaseGameTests : IDisposable
{
    private static readonly Guid HalfLifeId = Guid.NewGuid();
    private static readonly Guid NaturalSelectionId = Guid.NewGuid();
    private static readonly Guid GoldbergId = Guid.NewGuid();

    private readonly string _installRoot = Path.Combine(Path.GetTempPath(), $"lc-install-base-{Guid.NewGuid():N}");
    private readonly DbContextOptions _databaseOptions;
    private readonly HttpClient _httpClient;
    private readonly AuthenticationSession _session;
    private ServiceProvider? _services;

    public InstallServiceBaseGameTests()
    {
        Directory.CreateDirectory(_installRoot);

        _databaseOptions = new DbContextOptionsBuilder().UseInMemoryDatabase($"InstallServiceBaseGame-{Guid.NewGuid()}").Options;

        var halfLife = GameFactory.Make("Half-Life", HalfLifeId);
        var naturalSelection = GameFactory.Make("Natural Selection", NaturalSelectionId, type: GameType.Mod);
        naturalSelection.BaseGameId = HalfLifeId;

        using (var context = new DatabaseContext(NullLoggerFactory.Instance, _databaseOptions))
        {
            context.Games!.AddRange(halfLife, naturalSelection);
            context.SaveChanges();
        }

        var settings = TestSettingsProvider.Create();
        settings.CurrentValue.Authentication.ServerAddress = new Uri("https://server.test");
        settings.CurrentValue.Authentication.Token = new AuthToken
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(expires: DateTime.UtcNow.AddHours(1))),
            RefreshToken = "refresh-token",
            Expiration = DateTime.UtcNow.AddHours(1),
        };
        settings.CurrentValue.Games.InstallDirectories = [_installRoot];

        var tokens = new Mock<ITokenProvider>();
        tokens.Setup(p => p.GetToken()).Returns(() => settings.CurrentValue.Authentication.Token);

        _httpClient = new HttpClient(new CatalogHandler(ServerGames()));
        _session = new AuthenticationSession(tokens.Object, settings, NullLogger<AuthenticationSession>.Instance);

        Requests = new ApiRequestFactory(_httpClient, tokens.Object, settings, _session);
        Settings = settings;
    }

    private ApiRequestFactory Requests { get; }
    private ISettingsProvider Settings { get; }

    public void Dispose()
    {
        _services?.Dispose();
        _session.Dispose();
        _httpClient.Dispose();

        try { Directory.Delete(_installRoot, recursive: true); }
        catch (Exception) { }
    }

    [Fact]
    public async Task Reinstalling_a_mod_after_uninstalling_its_base_game_queues_the_base_game()
    {
        var subject = CreateSubject();
        var halfLifeDirectory = Path.Combine(_installRoot, "Half-Life");

        // Earlier in the session the queue saw Half-Life installed, so the mod went in on its own...
        await SetHalfLifeInstalledAsync(true);
        await ManifestHelper.WriteAsync(new SDK.Models.Manifest.Game { Id = HalfLifeId, Title = "Half-Life" }, halfLifeDirectory);

        await subject.Add(await FindAsync(NaturalSelectionId));

        subject.Queue.Skip(1).Select(i => i.Id).ShouldBe([NaturalSelectionId]);

        // ...then both were uninstalled outside the queue
        subject.Remove(NaturalSelectionId);
        Directory.Delete(halfLifeDirectory, recursive: true);
        await SetHalfLifeInstalledAsync(false);

        await subject.Add(await FindAsync(NaturalSelectionId));

        AssertQueuedInOrder(subject);
    }

    [Fact]
    public async Task A_base_game_carried_in_the_mods_plan_is_queued_as_itself()
    {
        var subject = CreateSubject();

        // The database says Half-Life is installed but its files are gone, so only the plan brings it in
        await SetHalfLifeInstalledAsync(true);

        await subject.Add(await FindAsync(NaturalSelectionId));

        AssertQueuedInOrder(subject);
    }

    [Fact]
    public async Task Loading_dependent_games_brings_in_addons_a_fresh_context_has_not_seen()
    {
        await using var context = new DatabaseContext(NullLoggerFactory.Instance, _databaseOptions);
        var gameService = new GameService(context, NullLogger<GameService>.Instance, null!, null!, null!, null!, null!, null!, null!, null!, null!);

        var halfLife = await gameService.GetAsync(HalfLifeId);
        halfLife.DependentGames.ShouldBeEmpty();

        await gameService.LoadDependentGamesAsync(halfLife);

        halfLife.DependentGames.Select(g => g.Id).ShouldBe([NaturalSelectionId]);
    }

    private async Task SetHalfLifeInstalledAsync(bool installed)
    {
        // Each change goes through its own context, as the UI's scopes do
        await using var context = new DatabaseContext(NullLoggerFactory.Instance, _databaseOptions);

        var halfLife = await context.Games!.FindAsync(HalfLifeId);
        halfLife!.Installed = installed;

        await context.SaveChangesAsync();
    }

    private async Task<Data.Models.Game> FindAsync(Guid id)
    {
        await using var context = new DatabaseContext(NullLoggerFactory.Instance, _databaseOptions);

        return (await context.Games!.FindAsync(id))!;
    }

    private void AssertQueuedInOrder(InstallService subject)
    {
        // The first entry is the placeholder holding the queue busy
        var queued = subject.Queue.Skip(1).ToList();

        queued.Select(i => (i.Id, i.Title, i.ItemType)).ShouldBe(
        [
            (HalfLifeId, "Half-Life", InstallPlanItemType.Game),
            (GoldbergId, "Goldberg", InstallPlanItemType.Redistributable),
            (NaturalSelectionId, "Natural Selection", InstallPlanItemType.Game),
        ]);

        queued[0].InstallDirectory.ShouldBe(Path.Combine(_installRoot, "Half-Life"));
        queued[0].Tasks.ShouldAllBe(t => t.TargetId == HalfLifeId);

        queued[2].InstallDirectory.ShouldBe(Path.Combine(_installRoot, "Half-Life", "ns"));
        queued[2].Tasks.ShouldAllBe(t => t.TargetId == NaturalSelectionId);
    }

    private InstallService CreateSubject()
    {
        // Each queue operation resolves its own GameService over its own context
        var services = new ServiceCollection();
        services.AddScoped(_ => new DatabaseContext(NullLoggerFactory.Instance, _databaseOptions));
        services.AddScoped(provider => new GameService(
            provider.GetRequiredService<DatabaseContext>(), NullLogger<GameService>.Instance,
            null!, null!, null!, null!, null!, null!, null!, null!, null!));
        _services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        var eventBus = new PluginEventBus(NullLogger<PluginEventBus>.Instance);
        var connection = new Mock<IConnectionClient>();
        connection.Setup(c => c.IsConnected()).Returns(true);

        var toolClient = new ToolClient(NullLogger<ToolClient>.Instance, Settings, Requests, null!);
        var redistributableClient = new RedistributableClient(NullLogger<RedistributableClient>.Instance, Settings, Requests, null!, null!);
        var gameClient = new GameClient(
            NullLogger<GameClient>.Instance, Requests, null!, null!, Settings, connection.Object,
            redistributableClient, null!, null!, null!, toolClient, eventBus, []);

        var subject = new InstallService(
            NullLogger<InstallService>.Instance, _services.GetRequiredService<IServiceScopeFactory>(), gameClient,
            redistributableClient, toolClient, null!, eventBus);

        // Something else is downloading, so Add only queues and never starts an install
        subject.Queue.Add(new InstallQueueGame(new Game { Id = Guid.NewGuid(), Title = "Busy" }) { Status = InstallStatus.Downloading });

        return subject;
    }

    private static Dictionary<Guid, Game> ServerGames() => new()
    {
        [HalfLifeId] = new Game
        {
            Id = HalfLifeId,
            Title = "Half-Life",
            Type = GameType.MainGame,
            Redistributables = [new Redistributable { Id = GoldbergId, Name = "Goldberg" }],
        },
        [NaturalSelectionId] = new Game
        {
            Id = NaturalSelectionId,
            Title = "Natural Selection",
            Type = GameType.Mod,
            BaseGameId = HalfLifeId,
            InstallTo = GameInstallLocation.SubDirectory,
            DirectoryName = "ns",
        },
    };

    /// <summary>Answers <c>/api/Games/{id}</c> from a fixed catalogue; everything else is not found.</summary>
    private sealed class CatalogHandler(Dictionary<Guid, Game> games) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var segments = request.RequestUri!.AbsolutePath.Trim('/').Split('/');

            if (segments is ["api", "Games", var id] && Guid.TryParse(id, out var gameId) && games.TryGetValue(gameId, out var game))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(game), Encoding.UTF8, "application/json"),
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
