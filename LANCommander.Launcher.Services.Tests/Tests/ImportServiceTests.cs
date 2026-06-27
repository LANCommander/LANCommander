using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LANCommander.Launcher.Data;
using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Services.Tests.Helpers;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using Xunit;
using Game = LANCommander.Launcher.Data.Models.Game;

namespace LANCommander.Launcher.Services.Tests.Tests;

public class ImportServiceTests
{
    private static DatabaseContext CreateContext() =>
        new(
            NullLoggerFactory.Instance,
            new DbContextOptionsBuilder()
                .UseInMemoryDatabase($"ImportServiceTests-{Guid.NewGuid()}")
                .Options);

    private static AuthenticationService CreateAuthService(Guid userId)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var jwt = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: "test",
            audience: "test",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5)));

        var tokenProvider = new Mock<ITokenProvider>();
        tokenProvider.Setup(p => p.GetToken()).Returns(new AuthToken { AccessToken = jwt });

        // Only the token-reading path (GetUserId) is exercised, so the rest of the graph is null!.
        return new AuthenticationService(
            tokenProvider.Object,
            settingsProvider: null!,
            scopeFactory: null!,
            connectionClient: null!,
            authenticationClient: null!,
            logger: NullLogger<AuthenticationService>.Instance);
    }

    private static ImportService CreateSubject(DatabaseContext context, Guid userId) =>
        // Reconciliation only touches the database context, the logger, and the authentication
        // service. The remaining ctor args are never dereferenced so we pass null! rather than
        // build mocks for the concrete SDK clients (which expose non-virtual methods anyway).
        new(
            NullLogger<ImportService>.Instance,
            importContextFactory: null!,
            gameClient: null!,
            toolClient: null!,
            libraryClient: null!,
            playSessionClient: null!,
            dbContext: context,
            gameService: null!,
            authenticationService: CreateAuthService(userId));

    private static async Task SeedLibraryAsync(DatabaseContext context, Guid userId, params Game[] games)
    {
        context.Libraries!.Add(new Library
        {
            UserId = userId,
            Games = games.ToList(),
        });

        await context.SaveChangesAsync();
    }

    private static async Task SeedCachedGamesAsync(DatabaseContext context, params Game[] games)
    {
        // Games that exist in the local database but are not associated with any library, mirroring
        // records left behind after they were dropped from the library on a previous import.
        context.Games!.AddRange(games);

        await context.SaveChangesAsync();
    }

    private static async Task<List<Guid>> GetLibraryGameIdsAsync(DatabaseContext context, Guid userId)
    {
        context.ChangeTracker.Clear();

        var library = await context.Libraries!
            .Include(l => l.Games)
            .FirstAsync(l => l.UserId == userId);

        return library.Games.Select(g => g.Id).ToList();
    }

    [Fact]
    public async Task ReconcileLibraryMembership_removes_local_games_missing_from_remote_library()
    {
        var userId = Guid.NewGuid();
        var keep = GameFactory.Make("Half-Life");
        var stale = GameFactory.Make("Removed From Depot");

        await using var context = CreateContext();
        await SeedLibraryAsync(context, userId, keep, stale);

        await CreateSubject(context, userId).ReconcileLibraryMembershipAsync([keep.Id]);

        var remaining = await GetLibraryGameIdsAsync(context, userId);
        remaining.ShouldBe([keep.Id]);
    }

    [Fact]
    public async Task ReconcileLibraryMembership_keeps_games_still_present_remotely()
    {
        var userId = Guid.NewGuid();
        var first = GameFactory.Make("Half-Life");
        var second = GameFactory.Make("Quake III Arena");

        await using var context = CreateContext();
        await SeedLibraryAsync(context, userId, first, second);

        await CreateSubject(context, userId).ReconcileLibraryMembershipAsync([first.Id, second.Id]);

        var remaining = await GetLibraryGameIdsAsync(context, userId);
        remaining.ShouldBe([first.Id, second.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task ReconcileLibraryMembership_skips_when_remote_library_is_empty()
    {
        // An empty remote list is ambiguous with a server-side failure, so the local
        // library must be left untouched rather than wiped.
        var userId = Guid.NewGuid();
        var game = GameFactory.Make("Half-Life");

        await using var context = CreateContext();
        await SeedLibraryAsync(context, userId, game);

        await CreateSubject(context, userId).ReconcileLibraryMembershipAsync([]);

        var remaining = await GetLibraryGameIdsAsync(context, userId);
        remaining.ShouldBe([game.Id]);
    }

    [Fact]
    public async Task ReconcileLibraryMembership_does_not_touch_other_users_libraries()
    {
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var ownGame = GameFactory.Make("Half-Life");
        var otherGame = GameFactory.Make("Quake III Arena");

        await using var context = CreateContext();
        await SeedLibraryAsync(context, userId, ownGame);
        await SeedLibraryAsync(context, otherUserId, otherGame);

        // Remote library for the current user no longer contains any games it shares with the
        // other user, but the other user's library must remain intact.
        await CreateSubject(context, userId).ReconcileLibraryMembershipAsync([ownGame.Id]);

        var otherRemaining = await GetLibraryGameIdsAsync(context, otherUserId);
        otherRemaining.ShouldBe([otherGame.Id]);
    }

    [Fact]
    public async Task ReconcileLibraryMembership_adds_cached_games_missing_from_library()
    {
        // Reproduces toggling "Enable User Libraries" off: the game was dropped from the library on
        // a previous import but its record is still cached, so a subsequent full library must
        // re-associate it instead of leaving it hidden.
        var userId = Guid.NewGuid();
        var inLibrary = GameFactory.Make("Half-Life");
        var cachedOnly = GameFactory.Make("Quake III Arena");

        await using var context = CreateContext();
        await SeedLibraryAsync(context, userId, inLibrary);
        await SeedCachedGamesAsync(context, cachedOnly);

        await CreateSubject(context, userId).ReconcileLibraryMembershipAsync([inLibrary.Id, cachedOnly.Id]);

        var remaining = await GetLibraryGameIdsAsync(context, userId);
        remaining.ShouldBe([inLibrary.Id, cachedOnly.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task ReconcileLibraryMembership_adds_and_removes_in_a_single_pass()
    {
        var userId = Guid.NewGuid();
        var keep = GameFactory.Make("Half-Life");
        var stale = GameFactory.Make("Removed From Depot");
        var cachedOnly = GameFactory.Make("Quake III Arena");

        await using var context = CreateContext();
        await SeedLibraryAsync(context, userId, keep, stale);
        await SeedCachedGamesAsync(context, cachedOnly);

        await CreateSubject(context, userId).ReconcileLibraryMembershipAsync([keep.Id, cachedOnly.Id]);

        var remaining = await GetLibraryGameIdsAsync(context, userId);
        remaining.ShouldBe([keep.Id, cachedOnly.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task ReconcileLibraryMembership_ignores_remote_games_not_cached_locally()
    {
        // A remote game whose record has not been imported yet cannot be added to the library here;
        // reconciliation must simply leave it out rather than fail.
        var userId = Guid.NewGuid();
        var inLibrary = GameFactory.Make("Half-Life");
        var notCachedRemoteId = Guid.NewGuid();

        await using var context = CreateContext();
        await SeedLibraryAsync(context, userId, inLibrary);

        await CreateSubject(context, userId).ReconcileLibraryMembershipAsync([inLibrary.Id, notCachedRemoteId]);

        var remaining = await GetLibraryGameIdsAsync(context, userId);
        remaining.ShouldBe([inLibrary.Id]);
    }
}
