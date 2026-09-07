using System.Security.Claims;
using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;
using LANCommander.Server.Data;
using LANCommander.Server.Endpoints;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using DataKey = LANCommander.Server.Data.Models.Key;
using DataUser = LANCommander.Server.Data.Models.User;
using DataGame = LANCommander.Server.Data.Models.Game;

namespace LANCommander.Server.Tests.Services;

/// <summary>
/// Covers CD key allocation end to end: the /api/Keys endpoints the launcher calls, the
/// KeyService allocate/release pair underneath them, and the reads the admin Keys editor
/// makes to report how many keys are in use.
/// <para>
/// The reports these tests were written for are "every key on a game is allocated even though
/// only a handful of machines ever installed it" and "a game ends up with one key value repeated
/// across every row", so most assertions are about how many keys a sequence of requests consumes
/// rather than about any single call's return value.
/// </para>
/// </summary>
[Collection("Application")]
public class KeyAllocationTests(ApplicationFixture fixture) : DalTest(fixture)
{
    #region Endpoint plumbing

    /// <summary>
    /// Runs one /api/Keys/GetAllocated request in its own DI scope, the way a real HTTP request
    /// would. The services carry per-instance query state, so sharing one scope across simulated
    /// requests would not reproduce production behaviour.
    /// </summary>
    private static async Task<SDK.Models.Key?> GetAllocatedAsync(Guid gameId, string? userName, string? macAddress = null)
    {
        await using var scope = ApplicationFixture.Instance.ServiceProvider.CreateAsyncScope();

        var result = await KeysEndpoints.GetAllocatedAsync(
            gameId,
            new KeyRequest { GameId = gameId, MacAddress = macAddress, ComputerName = "TEST-PC" },
            Principal(userName),
            scope.ServiceProvider.GetRequiredService<IMapper>(),
            scope.ServiceProvider.GetRequiredService<KeyService>(),
            scope.ServiceProvider.GetRequiredService<GameService>(),
            scope.ServiceProvider.GetRequiredService<UserService>(),
            scope.ServiceProvider.GetRequiredService<ILoggerFactory>());

        return (result as Ok<SDK.Models.Key>)?.Value;
    }

    private static async Task<SDK.Models.Key?> AllocateNewAsync(Guid gameId, string? userName, string? macAddress = null)
    {
        await using var scope = ApplicationFixture.Instance.ServiceProvider.CreateAsyncScope();

        var result = await KeysEndpoints.AllocateAsync(
            gameId,
            new KeyRequest { GameId = gameId, MacAddress = macAddress, ComputerName = "TEST-PC" },
            Principal(userName),
            scope.ServiceProvider.GetRequiredService<IMapper>(),
            scope.ServiceProvider.GetRequiredService<KeyService>(),
            scope.ServiceProvider.GetRequiredService<GameService>(),
            scope.ServiceProvider.GetRequiredService<UserService>(),
            scope.ServiceProvider.GetRequiredService<ILoggerFactory>());

        return (result as Ok<SDK.Models.Key>)?.Value;
    }

    private static ClaimsPrincipal Principal(string? userName) =>
        userName == null
            ? new ClaimsPrincipal(new ClaimsIdentity())
            : new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, userName)], "Test"));

    #endregion

    #region Fixtures

    private async Task<DataGame> AddGameWithKeysAsync(int keyCount, KeyAllocationMethod method)
    {
        var gameService = GetService<GameService>();

        var game = await gameService.AddAsync(new DataGame
        {
            Title = Unique("Game"),
            KeyAllocationMethod = method,
        });

        for (var i = 0; i < keyCount; i++)
            await GetService<KeyService>().AddAsync(new DataKey
            {
                Value = $"{game.Id:N}-KEY-{i:D3}",
                GameId = game.Id,
            });

        return game;
    }

    private async Task<DataUser> AddUserAsync()
    {
        var userService = GetService<UserService>();

        return await userService.AddAsync(
            new DataUser { UserName = Unique("player") },
            bypassPasswordPolicy: true,
            password: "Test1234!");
    }

    /// <summary>Reads the keys straight out of the database, navigations included, bypassing every service.</summary>
    private async Task<List<DataKey>> LoadKeysAsync(Guid gameId)
    {
        await using var context = await GetService<IDbContextFactory<DatabaseContext>>().CreateDbContextAsync();

        return await context.Set<DataKey>()
            .AsNoTracking()
            .Include(k => k.ClaimedByUser)
            .Where(k => k.GameId == gameId)
            .ToListAsync();
    }

    #endregion

    #region Repeat requests must not burn keys

    /// <summary>
    /// The launcher asks for an allocated key on install, on launch and whenever key change
    /// scripts run. For a user-account game every one of those calls has to hand back the key the
    /// user already holds; if it allocates a fresh one each time, a 100 key game is emptied by a
    /// handful of machines.
    /// </summary>
    [Fact]
    public async Task RepeatedRequestsFromTheSameUserReuseTheSameKey()
    {
        var game = await AddGameWithKeysAsync(10, KeyAllocationMethod.UserAccount);
        var user = await AddUserAsync();

        var first = await GetAllocatedAsync(game.Id, user.UserName);

        first.ShouldNotBeNull();
        first.Value.ShouldNotBeNullOrWhiteSpace();

        for (var i = 0; i < 5; i++)
            (await GetAllocatedAsync(game.Id, user.UserName))?.Value.ShouldBe(first.Value);

        var keys = await LoadKeysAsync(game.Id);

        keys.Count(k => k.AllocationMethod != null).ShouldBe(1);
    }

    [Fact]
    public async Task RepeatedRequestsFromTheSameMacAddressReuseTheSameKey()
    {
        var game = await AddGameWithKeysAsync(10, KeyAllocationMethod.MacAddress);
        var user = await AddUserAsync();

        var first = await GetAllocatedAsync(game.Id, user.UserName, "AA:BB:CC:DD:EE:FF");

        first.ShouldNotBeNull();

        for (var i = 0; i < 5; i++)
            (await GetAllocatedAsync(game.Id, user.UserName, "AA:BB:CC:DD:EE:FF"))?.Value.ShouldBe(first.Value);

        var keys = await LoadKeysAsync(game.Id);

        keys.Count(k => k.AllocationMethod != null).ShouldBe(1);
    }

    [Fact]
    public async Task DifferentUsersGetDifferentKeys()
    {
        var game = await AddGameWithKeysAsync(10, KeyAllocationMethod.UserAccount);

        var values = new List<string>();

        for (var i = 0; i < 3; i++)
        {
            var user = await AddUserAsync();
            var key = await GetAllocatedAsync(game.Id, user.UserName);

            key.ShouldNotBeNull();
            values.Add(key.Value);
        }

        values.Distinct().Count().ShouldBe(3);
    }

    /// <summary>
    /// Two machines belonging to the same user come online at once. Neither request sees the
    /// other's write, so both pick the same "first available" key. At minimum this must not
    /// consume two keys for one user.
    /// </summary>
    [Fact]
    public async Task ConcurrentRequestsFromTheSameUserDoNotConsumeTwoKeys()
    {
        var game = await AddGameWithKeysAsync(10, KeyAllocationMethod.UserAccount);
        var user = await AddUserAsync();

        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => GetAllocatedAsync(game.Id, user.UserName)));

        var keys = await LoadKeysAsync(game.Id);

        keys.Count(k => k.AllocationMethod != null).ShouldBe(1);
    }

    /// <summary>
    /// A request whose principal cannot be resolved to a user must not consume a key. Allocating
    /// one anyway writes AllocationMethod = UserAccount with no claimant, which
    /// <see cref="DataKey.IsAvailable"/> then treats as taken forever while
    /// <see cref="DataKey.IsAllocated"/> reports it as unclaimed: the key is burned and invisible.
    /// </summary>
    [Fact]
    public async Task RequestsWithAnUnresolvableUserDoNotBurnKeys()
    {
        var game = await AddGameWithKeysAsync(10, KeyAllocationMethod.UserAccount);

        await GetAllocatedAsync(game.Id, Unique("ghost"));

        var keys = await LoadKeysAsync(game.Id);

        keys.ShouldAllBe(k => k.AllocationMethod == null);
    }

    /// <summary>
    /// Same shape as above, but the anonymous principal path: no name claim at all.
    /// </summary>
    [Fact]
    public async Task RequestsWithNoIdentityDoNotBurnKeys()
    {
        var game = await AddGameWithKeysAsync(10, KeyAllocationMethod.UserAccount);

        await GetAllocatedAsync(game.Id, null);

        var keys = await LoadKeysAsync(game.Id);

        keys.ShouldAllBe(k => k.AllocationMethod == null);
    }

    /// <summary>
    /// Every key on a game already being spoken for must not silently hand a second user somebody
    /// else's key.
    /// </summary>
    [Fact]
    public async Task RequestsAgainstAnExhaustedGameReturnNothing()
    {
        var game = await AddGameWithKeysAsync(1, KeyAllocationMethod.UserAccount);

        var holder = await AddUserAsync();
        var latecomer = await AddUserAsync();

        var held = await GetAllocatedAsync(game.Id, holder.UserName);
        held.ShouldNotBeNull();

        var second = await GetAllocatedAsync(game.Id, latecomer.UserName);

        second.ShouldBeNull();
    }

    #endregion

    #region Release

    /// <summary>
    /// Releasing has to clear the claimant, not just the allocation method. ClaimedByUser has no
    /// mapped foreign key property, so the update path can only clear it through the navigation;
    /// if it doesn't, a released key stays attributed to its old owner in the database.
    /// </summary>
    [Fact]
    public async Task ReleasingAKeyClearsTheClaimant()
    {
        var game = await AddGameWithKeysAsync(3, KeyAllocationMethod.UserAccount);
        var user = await AddUserAsync();

        var allocated = await GetAllocatedAsync(game.Id, user.UserName);
        allocated.ShouldNotBeNull();

        await GetService<KeyService>().ReleaseAsync(allocated.Id);

        var released = (await LoadKeysAsync(game.Id)).Single(k => k.Id == allocated.Id);

        released.AllocationMethod.ShouldBeNull();
        released.ClaimedOn.ShouldBeNull();
        released.ClaimedByUser.ShouldBeNull();
        released.IsAvailable().ShouldBeTrue();
    }

    /// <summary>
    /// A key released from one user and picked up by another must not still carry the first user's
    /// claim, otherwise the original owner keeps matching it on their next request and two
    /// accounts end up sharing one key.
    /// </summary>
    [Fact]
    public async Task AReleasedKeyIsFullyTransferredToTheNextUser()
    {
        var game = await AddGameWithKeysAsync(1, KeyAllocationMethod.UserAccount);

        var first = await AddUserAsync();
        var second = await AddUserAsync();

        var allocated = await GetAllocatedAsync(game.Id, first.UserName);
        allocated.ShouldNotBeNull();

        await GetService<KeyService>().ReleaseAsync(allocated.Id);

        var reallocated = await GetAllocatedAsync(game.Id, second.UserName);
        reallocated.ShouldNotBeNull();
        reallocated.Id.ShouldBe(allocated.Id);

        var key = (await LoadKeysAsync(game.Id)).Single();

        key.ClaimedByUser.ShouldNotBeNull();
        key.ClaimedByUser.Id.ShouldBe(second.Id);

        // The original holder must not be handed the same key back.
        var firstAgain = await GetAllocatedAsync(game.Id, first.UserName);

        firstAgain.ShouldBeNull();
    }

    #endregion

    #region Explicit re-roll

    /// <summary>
    /// /api/Keys/Allocate is the "give me a different key" action. It should move the user onto a
    /// fresh key and hand the old one back to the pool, leaving exactly one key allocated.
    /// </summary>
    [Fact]
    public async Task ReRollingAKeyReleasesTheOldOne()
    {
        var game = await AddGameWithKeysAsync(5, KeyAllocationMethod.UserAccount);
        var user = await AddUserAsync();

        var original = await GetAllocatedAsync(game.Id, user.UserName);
        original.ShouldNotBeNull();

        var replacement = await AllocateNewAsync(game.Id, user.UserName);
        replacement.ShouldNotBeNull();
        replacement.Id.ShouldNotBe(original.Id);

        var keys = await LoadKeysAsync(game.Id);

        keys.Count(k => k.AllocationMethod != null).ShouldBe(1);
        keys.Single(k => k.Id == original.Id).IsAvailable().ShouldBeTrue();
    }

    #endregion

    #region What the admin Keys editor reads

    /// <summary>
    /// The Keys editor's Available/Allocated statistics come from
    /// <c>KeyService.GetAsync(k => k.GameId == id)</c> and <see cref="DataKey.IsAllocated"/>.
    /// IsAllocated tests the ClaimedByUser navigation, so the read has to load it or the page
    /// reports every user-account key as free while the grid below it shows them claimed.
    /// </summary>
    [Fact]
    public async Task TheKeysEditorReadReportsAllocatedKeysAsAllocated()
    {
        var game = await AddGameWithKeysAsync(4, KeyAllocationMethod.UserAccount);
        var user = await AddUserAsync();

        (await GetAllocatedAsync(game.Id, user.UserName)).ShouldNotBeNull();

        var keys = await GetService<KeyService>().GetAsync(k => k.GameId == game.Id);

        keys.Count(k => k.IsAllocated()).ShouldBe(1);
    }

    /// <summary>
    /// Keys must stay distinguishable from one another. A game whose rows all carry the same
    /// value is the "one key repeating for all entries" report.
    /// </summary>
    [Fact]
    public async Task KeysKeepDistinctValues()
    {
        var game = await AddGameWithKeysAsync(20, KeyAllocationMethod.UserAccount);

        var keys = await LoadKeysAsync(game.Id);

        keys.Count.ShouldBe(20);
        keys.ShouldAllBe(k => k.Value != null);
        keys.Select(k => k.Value).Distinct().Count().ShouldBe(20);
    }

    #endregion

    #region Saving unrelated parts of a game

    /// <summary>
    /// Saving a game from a view that never loaded its keys (details, media, actions, ...) must
    /// leave the key rows exactly as they were. GameService.UpdateAsync syncs the Keys collection
    /// on every save, so an unloaded navigation must not be read as "the user removed them all".
    /// </summary>
    [Fact]
    public async Task SavingAGameWithoutLoadingItsKeysLeavesThemAlone()
    {
        var game = await AddGameWithKeysAsync(6, KeyAllocationMethod.UserAccount);
        var gameService = GetService<GameService>();

        var before = await LoadKeysAsync(game.Id);
        before.Count.ShouldBe(6);

        var reloaded = await gameService.GetAsync(game.Id);
        reloaded.Description = "edited somewhere else";

        await gameService.UpdateAsync(reloaded);

        var after = await LoadKeysAsync(game.Id);

        after.Count.ShouldBe(6);
        after.Select(k => k.Id).OrderBy(id => id).ShouldBe(before.Select(k => k.Id).OrderBy(id => id));
        after.Select(k => k.Value).Distinct().Count().ShouldBe(6);
    }

    /// <summary>
    /// Same save, but from a view that did load the keys. Round-tripping them must not duplicate
    /// the rows or collapse their values.
    /// </summary>
    [Fact]
    public async Task SavingAGameWithItsKeysLoadedDoesNotDuplicateThem()
    {
        var game = await AddGameWithKeysAsync(6, KeyAllocationMethod.UserAccount);
        var gameService = GetService<GameService>();

        var before = await LoadKeysAsync(game.Id);

        var reloaded = await gameService
            .Query(q => q.Include(g => g.Keys))
            .GetAsync(game.Id);

        reloaded.Keys.Count.ShouldBe(6);
        reloaded.Description = "edited with keys loaded";

        await gameService.UpdateAsync(reloaded);

        var after = await LoadKeysAsync(game.Id);

        after.Count.ShouldBe(6);
        after.Select(k => k.Id).OrderBy(id => id).ShouldBe(before.Select(k => k.Id).OrderBy(id => id));
        after.Select(k => k.Value).Distinct().Count().ShouldBe(6);
    }

    /// <summary>
    /// An allocation must survive a save of the game it belongs to. If the collection sync
    /// re-writes the key rows from a detached copy that never loaded ClaimedByUser, the claim is
    /// silently dropped and the launcher allocates another key on its next run.
    /// </summary>
    [Fact]
    public async Task SavingAGameKeepsExistingAllocations()
    {
        var game = await AddGameWithKeysAsync(6, KeyAllocationMethod.UserAccount);
        var user = await AddUserAsync();
        var gameService = GetService<GameService>();

        var allocated = await GetAllocatedAsync(game.Id, user.UserName);
        allocated.ShouldNotBeNull();

        var reloaded = await gameService
            .Query(q => q.Include(g => g.Keys))
            .GetAsync(game.Id);

        reloaded.Description = "edited after allocation";

        await gameService.UpdateAsync(reloaded);

        var key = (await LoadKeysAsync(game.Id)).Single(k => k.Id == allocated.Id);

        key.AllocationMethod.ShouldBe(KeyAllocationMethod.UserAccount);
        key.ClaimedByUser.ShouldNotBeNull();
        key.ClaimedByUser.Id.ShouldBe(user.Id);

        // ...and the launcher still gets the same key back afterwards.
        (await GetAllocatedAsync(game.Id, user.UserName))?.Value.ShouldBe(allocated.Value);
    }

    #endregion
}
