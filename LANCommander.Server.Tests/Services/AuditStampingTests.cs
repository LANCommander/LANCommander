using System.Security.Claims;
using LANCommander.SDK.Enums;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using GameAction = LANCommander.Server.Data.Models.Action;

namespace LANCommander.Server.Tests.Services;

/// <summary>
/// Every inserted row records who created it, however it was inserted: through a service, directly
/// through a DbContext, or as a child added through a navigation collection.
/// </summary>
[Collection("Application")]
public class AuditStampingTests(ApplicationFixture fixture) : DalTest(fixture)
{
    async Task<Guid> AddUserAsync()
    {
        var id = Guid.NewGuid();

        await using var db = await CreateDbContextAsync();

        db.Users.Add(new User { Id = id, UserName = Unique("user") });

        await db.SaveChangesAsync();

        return id;
    }

    Task<DatabaseContext> CreateDbContextAsync() =>
        GetService<IDbContextFactory<DatabaseContext>>().CreateDbContextAsync();

    /// <summary>Signs <paramref name="userId"/> in for the rest of the calling flow, as a request or circuit would.</summary>
    IDisposable SignIn(Guid userId)
    {
        var accessor = GetService<IHttpContextAccessor>();

        accessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Name, "test")],
                authenticationType: "Test")),
        };

        return new SignOut(accessor);
    }

    sealed class SignOut(IHttpContextAccessor accessor) : IDisposable
    {
        public void Dispose() => accessor.HttpContext = null;
    }

    [Fact]
    public async Task RowsAddedDirectlyThroughAContextRecordTheSignedInUser()
    {
        var userId = await AddUserAsync();
        var game = await AddGameAsync();
        var versionId = Guid.NewGuid();
        var scriptId = Guid.NewGuid();

        using (SignIn(userId))
        {
            await using var db = await CreateDbContextAsync();

            db.GameVersions.Add(new GameVersion
            {
                Id = versionId,
                GameId = game.Id,
                Version = "1.0",
                Scripts = [new Script { Id = scriptId, GameId = game.Id, Name = Unique("Script"), Contents = "", Type = ScriptType.Install }],
            });

            await db.SaveChangesAsync();
        }

        await using var check = await CreateDbContextAsync();

        var version = await check.GameVersions.SingleAsync(v => v.Id == versionId);
        var script = await check.Set<Script>().SingleAsync(s => s.Id == scriptId);

        version.CreatedById.ShouldBe(userId);
        version.CreatedOn.ShouldNotBe(default);
        script.CreatedById.ShouldBe(userId);
    }

    [Fact]
    public async Task ChildrenAddedThroughANavigationOfATrackedEntityRecordTheSignedInUser()
    {
        var userId = await AddUserAsync();
        var game = await AddGameAsync();
        var actionName = Unique("Action");

        using (SignIn(userId))
        {
            await using var db = await CreateDbContextAsync();

            var tracked = await db.Games.Include(g => g.Actions).SingleAsync(g => g.Id == game.Id);

            // No key set: a child found through a navigation with a key already set is taken as an existing row
            tracked.Actions!.Add(new GameAction { Name = actionName, Path = "game.exe" });

            await db.SaveChangesAsync();
        }

        await using var check = await CreateDbContextAsync();

        (await check.Set<GameAction>().SingleAsync(a => a.Name == actionName)).CreatedById.ShouldBe(userId);
    }

    [Fact]
    public async Task CreatingAGameVersionRecordsTheSignedInUser()
    {
        var userId = await AddUserAsync();
        var game = await AddGameAsync();

        GameVersion version;

        using (SignIn(userId))
            version = await GetService<GameVersionService>().CreateAsync(game.Id, "1.0", null);

        await using var check = await CreateDbContextAsync();

        (await check.GameVersions.SingleAsync(v => v.Id == version.Id)).CreatedById.ShouldBe(userId);
    }

    [Fact]
    public async Task RowsSavedWithNoSignedInUserHaveNoCreatorButACreationDate()
    {
        var game = await AddGameAsync();
        var versionId = Guid.NewGuid();

        await using (var db = await CreateDbContextAsync())
        {
            db.GameVersions.Add(new GameVersion { Id = versionId, GameId = game.Id, Version = "1.0" });

            await db.SaveChangesAsync();
        }

        await using var check = await CreateDbContextAsync();

        var version = await check.GameVersions.SingleAsync(v => v.Id == versionId);

        version.CreatedById.ShouldBeNull();
        version.CreatedOn.ShouldNotBe(default);
    }

    [Fact]
    public async Task AUserIdThatIsNotALocalUserIsNotRecorded()
    {
        var game = await AddGameAsync();
        var versionId = Guid.NewGuid();

        using (SignIn(Guid.NewGuid()))
        {
            await using var db = await CreateDbContextAsync();

            db.GameVersions.Add(new GameVersion { Id = versionId, GameId = game.Id, Version = "1.0" });

            await db.SaveChangesAsync();
        }

        await using var check = await CreateDbContextAsync();

        (await check.GameVersions.SingleAsync(v => v.Id == versionId)).CreatedById.ShouldBeNull();
    }

    [Fact]
    public async Task AnExplicitCreatorIsKept()
    {
        var signedIn = await AddUserAsync();
        var creator = await AddUserAsync();
        var game = await AddGameAsync();
        var versionId = Guid.NewGuid();

        using (SignIn(signedIn))
        {
            await using var db = await CreateDbContextAsync();

            db.GameVersions.Add(new GameVersion { Id = versionId, GameId = game.Id, Version = "1.0", CreatedById = creator });

            await db.SaveChangesAsync();
        }

        await using var check = await CreateDbContextAsync();

        (await check.GameVersions.SingleAsync(v => v.Id == versionId)).CreatedById.ShouldBe(creator);
    }

    [Fact]
    public async Task RowsUpdatedDirectlyThroughAContextRecordTheSignedInUser()
    {
        var userId = await AddUserAsync();
        var game = await AddGameAsync();
        var before = DateTime.UtcNow;

        using (SignIn(userId))
        {
            await using var db = await CreateDbContextAsync();

            (await db.Games.SingleAsync(g => g.Id == game.Id)).Notes = "Changed";

            await db.SaveChangesAsync();
        }

        await using var check = await CreateDbContextAsync();

        var updated = await check.Games.SingleAsync(g => g.Id == game.Id);

        updated.Notes.ShouldBe("Changed");
        updated.UpdatedById.ShouldBe(userId);
        updated.UpdatedOn.ShouldBeGreaterThanOrEqualTo(before);
        updated.CreatedById.ShouldBe(game.CreatedById);
    }

    [Fact]
    public async Task UpdatingThroughAServiceRecordsTheSignedInUser()
    {
        var userId = await AddUserAsync();
        var game = await AddGameAsync();
        var gameService = GetService<GameService>();

        using (SignIn(userId))
        {
            var loaded = await gameService.GetAsync(game.Id);

            loaded.Notes = "Changed";

            await gameService.UpdateAsync(loaded);
        }

        await using var check = await CreateDbContextAsync();

        (await check.Games.SingleAsync(g => g.Id == game.Id)).UpdatedById.ShouldBe(userId);
    }

    [Fact]
    public async Task AnExplicitEditorIsKept()
    {
        var signedIn = await AddUserAsync();
        var editor = await AddUserAsync();
        var game = await AddGameAsync();
        var updatedOn = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        using (SignIn(signedIn))
        {
            await using var db = await CreateDbContextAsync();

            var tracked = await db.Games.SingleAsync(g => g.Id == game.Id);

            tracked.Notes = "Changed";
            tracked.UpdatedById = editor;
            tracked.UpdatedOn = updatedOn;

            await db.SaveChangesAsync();
        }

        await using var check = await CreateDbContextAsync();

        var updated = await check.Games.SingleAsync(g => g.Id == game.Id);

        updated.UpdatedById.ShouldBe(editor);
        updated.UpdatedOn.ShouldBe(updatedOn);
    }

    [Fact]
    public async Task RowsUpdatedWithNoSignedInUserKeepTheirEditorButGetANewDate()
    {
        var editor = await AddUserAsync();
        var game = await AddGameAsync();

        using (SignIn(editor))
        {
            await using var db = await CreateDbContextAsync();

            (await db.Games.SingleAsync(g => g.Id == game.Id)).Notes = "First";

            await db.SaveChangesAsync();
        }

        var before = DateTime.UtcNow;

        await using (var db = await CreateDbContextAsync())
        {
            (await db.Games.SingleAsync(g => g.Id == game.Id)).Notes = "Second";

            await db.SaveChangesAsync();
        }

        await using var check = await CreateDbContextAsync();

        var updated = await check.Games.SingleAsync(g => g.Id == game.Id);

        updated.UpdatedById.ShouldBe(editor);
        updated.UpdatedOn.ShouldBeGreaterThanOrEqualTo(before);
    }
}
