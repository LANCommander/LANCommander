using Shouldly;
using LANCommander.Server.Services;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.SDK.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LANCommander.Server.Tests.Data;

[Collection("Application")]
public class VersioningTests(ApplicationFixture fixture) : BaseTest(fixture)
{
    private async Task<Game> SeedGameAsync(Action<Game, DatabaseContext> configure = null)
    {
        var contextFactory = GetService<IDbContextFactory<DatabaseContext>>();

        await using var context = await contextFactory.CreateDbContextAsync();

        var game = new Game
        {
            Id = Guid.NewGuid(),
            Title = $"Test Game {Guid.NewGuid()}",
            CreatedOn = DateTime.UtcNow,
        };

        context.Games.Add(game);

        configure?.Invoke(game, context);

        await context.SaveChangesAsync();

        return game;
    }

    [Fact]
    public async Task BackfillCreatesOneVersionPerArchiveWithConfigOnNewest()
    {
        var contextFactory = GetService<IDbContextFactory<DatabaseContext>>();

        var olderArchiveCreatedOn = DateTime.UtcNow.AddDays(-2);
        var newerArchiveCreatedOn = DateTime.UtcNow.AddDays(-1);

        var game = await SeedGameAsync((g, db) =>
        {
            var storageLocation = new StorageLocation
            {
                Id = Guid.NewGuid(),
                Path = "archives",
                Type = StorageLocationType.Archive,
                Default = true,
            };

            db.StorageLocations.Add(storageLocation);

            db.Set<Archive>().Add(new Archive
            {
                Id = Guid.NewGuid(),
                GameId = g.Id,
                StorageLocationId = storageLocation.Id,
                Version = "1.0.0",
                ObjectKey = "archive-1.zip",
                CreatedOn = olderArchiveCreatedOn,
            });

            db.Set<Archive>().Add(new Archive
            {
                Id = Guid.NewGuid(),
                GameId = g.Id,
                StorageLocationId = storageLocation.Id,
                Version = "2.0.0",
                ObjectKey = "archive-2.zip",
                CreatedOn = newerArchiveCreatedOn,
            });

            db.Set<Script>().Add(new Script
            {
                Id = Guid.NewGuid(),
                GameId = g.Id,
                Name = "Install Script",
                Contents = "echo hello",
                Type = ScriptType.Install,
            });

            db.Set<LANCommander.Server.Data.Models.Action>().Add(new LANCommander.Server.Data.Models.Action
            {
                Id = Guid.NewGuid(),
                GameId = g.Id,
                Name = "Launch",
            });

            db.Set<SavePath>().Add(new SavePath
            {
                Id = Guid.NewGuid(),
                GameId = g.Id,
                Type = SavePathType.File,
                Path = "save/*.sav",
            });
        });

        await GameVersionBackfill.RunAsync(contextFactory, NullLogger.Instance);

        await using var verifyContext = await contextFactory.CreateDbContextAsync();

        var versions = await verifyContext.GameVersions
            .Include(v => v.Archive)
            .Where(v => v.GameId == game.Id)
            .OrderBy(v => v.SortOrder)
            .ToListAsync();

        versions.Count.ShouldBe(2);
        versions[0].Version.ShouldBe("1.0.0");
        versions[0].SortOrder.ShouldBe(0);
        versions[1].Version.ShouldBe("2.0.0");
        versions[1].SortOrder.ShouldBe(1);

        // Each archive is linked to its own version.
        versions[0].Archive.ShouldNotBeNull();
        versions[0].Archive.Version.ShouldBe("1.0.0");
        versions[1].Archive.ShouldNotBeNull();
        versions[1].Archive.Version.ShouldBe("2.0.0");

        var newest = versions[1];

        var script = await verifyContext.Set<Script>().FirstAsync(s => s.GameId == game.Id);
        var action = await verifyContext.Set<LANCommander.Server.Data.Models.Action>().FirstAsync(a => a.GameId == game.Id);
        var savePath = await verifyContext.Set<SavePath>().FirstAsync(p => p.GameId == game.Id);

        // Config is attached to the newest version (dual-written: GameId preserved).
        script.GameVersionId.ShouldBe(newest.Id);
        action.GameVersionId.ShouldBe(newest.Id);
        savePath.GameVersionId.ShouldBe(newest.Id);
    }

    [Fact]
    public async Task BackfillCreatesEmptyVersionForGameWithoutArchives()
    {
        var contextFactory = GetService<IDbContextFactory<DatabaseContext>>();

        var game = await SeedGameAsync();

        await GameVersionBackfill.RunAsync(contextFactory, NullLogger.Instance);

        await using var verifyContext = await contextFactory.CreateDbContextAsync();

        var versions = await verifyContext.GameVersions
            .Where(v => v.GameId == game.Id)
            .ToListAsync();

        versions.Count.ShouldBe(1);
        versions[0].Version.ShouldBe(string.Empty);
        versions[0].SortOrder.ShouldBe(0);
    }

    [Fact]
    public async Task GetOrCreateLatestAsyncCreatesEmptyVersionWhenNoneExists()
    {
        var gameVersionService = GetService<GameVersionService>();

        var game = await SeedGameAsync();

        var version = await gameVersionService.GetOrCreateLatestAsync(game.Id);

        version.ShouldNotBeNull();
        version.GameId.ShouldBe(game.Id);
        version.Version.ShouldBe(string.Empty);
        version.SortOrder.ShouldBe(0);

        // A subsequent call returns the same version rather than creating another.
        var again = await gameVersionService.GetOrCreateLatestAsync(game.Id);
        again.Id.ShouldBe(version.Id);
    }

    [Fact]
    public async Task CreateAsyncCopiesConfigIntoNewRows()
    {
        var contextFactory = GetService<IDbContextFactory<DatabaseContext>>();
        var gameVersionService = GetService<GameVersionService>();

        var game = await SeedGameAsync();

        // Seed an initial version carrying version-scoped config.
        var firstVersion = await gameVersionService.GetOrCreateLatestAsync(game.Id);

        Guid originalScriptId;

        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            var script = new Script
            {
                Id = Guid.NewGuid(),
                GameId = game.Id,
                GameVersionId = firstVersion.Id,
                Name = "Install Script",
                Contents = "echo hello",
                Type = ScriptType.Install,
            };

            originalScriptId = script.Id;

            context.Set<Script>().Add(script);
            await context.SaveChangesAsync();
        }

        // Creating a new version copies the prior version's config into fresh rows.
        var secondVersion = await gameVersionService.CreateAsync(game.Id, "2.0.0", "New version");

        secondVersion.SortOrder.ShouldBe(firstVersion.SortOrder + 1);

        await using var verifyContext = await contextFactory.CreateDbContextAsync();

        var copiedScripts = await verifyContext.Set<Script>()
            .Where(s => s.GameVersionId == secondVersion.Id)
            .ToListAsync();

        copiedScripts.Count.ShouldBe(1);
        copiedScripts[0].Id.ShouldNotBe(originalScriptId); // a new row, not a re-parent
        copiedScripts[0].Name.ShouldBe("Install Script");
        copiedScripts[0].Contents.ShouldBe("echo hello");
        copiedScripts[0].GameId.ShouldBe(game.Id); // dual-written

        // The original version's config is left intact.
        var originalScript = await verifyContext.Set<Script>().FirstAsync(s => s.Id == originalScriptId);
        originalScript.GameVersionId.ShouldBe(firstVersion.Id);
    }

    [Fact]
    public async Task GetNewerThanAsyncReturnsVersionsAfterInstalled()
    {
        var contextFactory = GetService<IDbContextFactory<DatabaseContext>>();
        var gameVersionService = GetService<GameVersionService>();

        var game = await SeedGameAsync((g, db) =>
        {
            for (var i = 1; i <= 3; i++)
            {
                var version = new GameVersion
                {
                    Id = Guid.NewGuid(),
                    GameId = g.Id,
                    Version = $"{i}.0.0",
                    SortOrder = i - 1,
                    CreatedOn = DateTime.UtcNow.AddDays(-3 + i),
                };

                version.Archive = new Archive
                {
                    Id = Guid.NewGuid(),
                    GameId = g.Id,
                    Version = $"{i}.0.0",
                    ObjectKey = $"archive-{i}.zip",
                    CreatedOn = version.CreatedOn,
                };

                db.GameVersions.Add(version);
            }
        });

        var newerThanFirst = (await gameVersionService.GetNewerThanAsync(game.Id, "1.0.0")).ToList();
        newerThanFirst.Select(v => v.Version).ShouldBe(new[] { "2.0.0", "3.0.0" });

        // A blank installed version yields just the latest.
        var blank = (await gameVersionService.GetNewerThanAsync(game.Id, string.Empty)).ToList();
        blank.Select(v => v.Version).ShouldBe(new[] { "3.0.0" });

        // An unknown installed version also yields just the latest.
        var unknown = (await gameVersionService.GetNewerThanAsync(game.Id, "9.9.9")).ToList();
        unknown.Select(v => v.Version).ShouldBe(new[] { "3.0.0" });

        // The latest version yields nothing newer.
        var newerThanLatest = (await gameVersionService.GetNewerThanAsync(game.Id, "3.0.0")).ToList();
        newerThanLatest.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetLatestAsyncResolvesHighestSortOrder()
    {
        var gameVersionService = GetService<GameVersionService>();

        var game = await SeedGameAsync((g, db) =>
        {
            db.GameVersions.Add(new GameVersion
            {
                Id = Guid.NewGuid(),
                GameId = g.Id,
                Version = "1.0.0",
                SortOrder = 0,
                CreatedOn = DateTime.UtcNow.AddDays(-2),
            });

            db.GameVersions.Add(new GameVersion
            {
                Id = Guid.NewGuid(),
                GameId = g.Id,
                Version = "2.0.0",
                SortOrder = 1,
                CreatedOn = DateTime.UtcNow.AddDays(-1),
            });
        });

        var latest = await gameVersionService.GetLatestAsync(game.Id);

        latest.ShouldNotBeNull();
        latest.Version.ShouldBe("2.0.0");
        latest.SortOrder.ShouldBe(1);
    }

    // Quarantined: depends on the removed monolithic SDK.Client facade (Client.Tags).
    // Needs rewiring to the per-domain DI clients introduced in commit 1936f505.
    [Fact(Skip = "Pending migration to per-domain SDK clients (monolithic SDK.Client removed)")]
    public async Task CreatedByShouldWork()
    {
        await Task.CompletedTask;
        /*
        // Simple service that's not bound to change much
        var tagService = GetService<TagService>();

        var user = await EnsureAdminUserCreatedAsync();

        await AuthenticateAsync(TestConstants.AdminUserName, TestConstants.AdminInitialPassword);

        var response = await TagClient.CreateAsync(new SDK.Models.Tag
        {
            Name = "Test Tag",
        });

        var tag = await tagService
            .Include(t => t.CreatedBy)
            .GetAsync(response.Id);

        tag.Name.ShouldBe("Test Tag");
        tag.CreatedById.ShouldBe(user.Id);
        tag.CreatedBy.ShouldNotBeNull();
        tag.CreatedBy.UserName.ShouldBe(user.UserName);
        tag.CreatedBy.Id.ShouldBe(user.Id);
        */
    }
}