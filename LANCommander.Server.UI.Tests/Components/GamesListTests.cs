using Bunit;
using LANCommander.SDK.Enums;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using LANCommander.Server.UI.Pages.Games.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>The Games list's filters and formatting, in memory: no database needed.</summary>
public class GameFiltersTests
{
    private static Game GameWith(string? description = "A game", params MediaType[] media) => new()
    {
        Id = Guid.NewGuid(),
        Title = "Neon Drift",
        Description = description,
        Media = media.Select(type => new Media { Type = type, Crc32 = "" }).ToList(),
    };

    private static bool Matches(GameFacet facet, Game game, params string[] values) =>
        GameFilters.Matches(facet, values).Compile()(game);

    [Theory]
    [InlineData("Cover", MediaType.Icon)]
    [InlineData("Icon", MediaType.Cover)]
    [InlineData("Background", MediaType.Cover)]
    [InlineData("Logo", MediaType.Background)]
    public void Missing_MatchesGamesWithoutThatArt(string value, MediaType other)
    {
        Assert.True(Matches(GameFacet.Missing, GameWith("A game", other), value));
        Assert.False(Matches(GameFacet.Missing, GameWith("A game", Enum.Parse<MediaType>(value), other), value));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("A game", false)]
    public void MissingDescription_MatchesBlankDescriptions(string? description, bool missing)
    {
        Assert.Equal(missing, Matches(GameFacet.Missing, GameWith(description), "Description"));
    }

    [Fact]
    public void Missing_WithSeveralValues_MatchesAnyGap()
    {
        var noLogo = GameWith("A game", MediaType.Cover, MediaType.Icon, MediaType.Background);
        var complete = GameWith("A game", MediaType.Cover, MediaType.Icon, MediaType.Background, MediaType.Logo);

        Assert.True(Matches(GameFacet.Missing, noLogo, "Cover", "Logo"));
        Assert.False(Matches(GameFacet.Missing, complete, "Cover", "Logo"));
    }

    [Fact]
    public void MissingArtView_CoversCoverIconBackgroundAndLogo()
    {
        var view = GameFilters.Views.Single(v => v.Key == "missing-art").Predicate!.Compile();

        Assert.False(view(GameWith("A game", MediaType.Cover, MediaType.Icon, MediaType.Background, MediaType.Logo)));
        Assert.True(view(GameWith("A game", MediaType.Cover, MediaType.Icon, MediaType.Background)));
        Assert.True(view(GameWith("A game", MediaType.Icon, MediaType.Background, MediaType.Logo)));

        // A missing description isn't missing art
        Assert.False(view(GameWith(null, MediaType.Cover, MediaType.Icon, MediaType.Background, MediaType.Logo)));
    }

    [Fact]
    public void Filters_CombineMissingWithOtherFacets()
    {
        var filters = new GameFilters().Toggle(GameFacet.Missing, "Cover").Toggle(GameFacet.Type, nameof(GameType.MainGame));
        var predicate = filters.ToExpression().Compile();

        var game = GameWith("A game", MediaType.Icon);
        game.Type = GameType.MainGame;

        Assert.True(predicate(game));

        game.Type = GameType.Mod;
        game.BaseGameId = null;

        Assert.False(predicate(game));
        Assert.Equal(1, filters.CountOf(GameFacet.Missing));
        Assert.True(filters.WithoutFacets().IsEmpty);
    }

    [Fact]
    public void ArchiveSize_IsTheLatestArchive()
    {
        var game = GameWith();
        game.Archives =
        [
            new Archive { CompressedSize = 100, CreatedOn = new DateTime(2026, 1, 1), Version = "1" },
            new Archive { CompressedSize = 250, CreatedOn = new DateTime(2026, 3, 1), Version = "2" },
            new Archive { CompressedSize = 175, CreatedOn = new DateTime(2026, 2, 1), Version = "1.5" },
        ];

        Assert.Equal(250, GameListFormat.ArchiveSize(game));

        game.Archives = [];
        Assert.Null(GameListFormat.ArchiveSize(game));
    }

    [Theory]
    [InlineData(30, "just now")]
    [InlineData(60 * 5, "5m ago")]
    [InlineData(60 * 60 * 3, "3h ago")]
    [InlineData(60 * 60 * 24 * 3, "3d ago")]
    [InlineData(60 * 60 * 24 * 14, "2w ago")]
    [InlineData(60 * 60 * 24 * 45, "1mo ago")]
    [InlineData(60 * 60 * 24 * 800, "2y ago")]
    public void Ago_IsShort(int secondsAgo, string expected)
    {
        var now = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal(expected, GameListFormat.Ago(now.AddSeconds(-secondsAgo), now));
    }

    [Theory]
    [InlineData("Age of Empires II: HD Edition", "AE")]
    [InlineData("Call of Duty 4", "CD")]
    [InlineData("Borderlands 2", "B2")]
    [InlineData("Quake", "QU")]
    [InlineData("", "")]
    public void Initials_TakeTwoLetters(string title, string expected)
    {
        Assert.Equal(expected, GameListFormat.Initials(title));
    }

    [Fact]
    public void ChildSummary_NamesTheKind()
    {
        Assert.Equal("2 expansions", GameListFormat.ChildSummary([new Game { Type = GameType.Expansion }, new Game { Type = GameType.Expansion }]));
        Assert.Equal("1 mod", GameListFormat.ChildSummary([new Game { Type = GameType.Mod }]));
        Assert.Equal("2 add-ons", GameListFormat.ChildSummary([new Game { Type = GameType.Mod }, new Game { Type = GameType.Expansion }]));
    }

    [Fact]
    public void Size_UsesOneDecimalAndAUnit()
    {
        Assert.Equal("2.1 GB", GameListFormat.Size(2_100_000_000));
        Assert.Equal("412 MB", GameListFormat.Size(412_000_000));
    }
}

/// <summary>The Games list's queries against the real SQLite database: the archive column and the library size.</summary>
[Collection("BUnit")]
public class GamesListDatabaseTests(BUnitServerFixture fixture) : BUnitTestContext(fixture)
{
    private async Task<(Guid GameId, Guid[] ArchiveIds)> SeedGameWithArchivesAsync(DatabaseContext context, params (long Size, DateTime CreatedOn)[] archives)
    {
        var storageLocation = await context.Set<StorageLocation>().FirstAsync(s => s.Type == StorageLocationType.Archive);

        var game = new Game { Id = Guid.NewGuid(), Title = $"Size Test {Guid.NewGuid():N}", Type = GameType.MainGame };
        context.Add(game);

        var entities = archives.Select(a => new Archive
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            StorageLocationId = storageLocation.Id,
            ObjectKey = Guid.NewGuid().ToString(),
            Version = a.CreatedOn.ToString("yyyyMMdd"),
            CompressedSize = a.Size,
            CreatedOn = a.CreatedOn,
            UpdatedOn = a.CreatedOn,
        }).ToList();

        context.AddRange(entities);
        await context.SaveChangesAsync();

        return (game.Id, entities.Select(e => e.Id).ToArray());
    }

    private static async Task RemoveAsync(DatabaseContext context, Guid gameId)
    {
        await context.Set<Archive>().Where(a => a.GameId == gameId).ExecuteDeleteAsync();
        await context.Set<Game>().Where(g => g.Id == gameId).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task ArchiveColumn_SortsInSqlite()
    {
        var factory = Fixture.Factory.RealServices.GetRequiredService<IDbContextFactory<DatabaseContext>>();
        await using var context = await factory.CreateDbContextAsync();

        var (big, _) = await SeedGameWithArchivesAsync(context, (100, new DateTime(2026, 1, 1)), (9_000, new DateTime(2026, 2, 1)));
        var (small, _) = await SeedGameWithArchivesAsync(context, (5_000, new DateTime(2026, 1, 1)), (10, new DateTime(2026, 2, 1)));

        try
        {
            var query = context.Set<Game>().AsNoTracking()
                .Where(g => g.Id == big || g.Id == small)
                .OrderByDescending(GameListFormat.LatestArchiveSize);

            // A correlated subquery, not client evaluation
            Assert.Contains("LIMIT 1", query.ToQueryString());

            var ids = await query.Select(g => g.Id).ToListAsync();

            Assert.Equal([big, small], ids);
        }
        finally
        {
            await RemoveAsync(context, big);
            await RemoveAsync(context, small);
        }
    }

    [Fact]
    public async Task LibrarySize_SumsEachGamesLatestArchive()
    {
        var factory = Fixture.Factory.RealServices.GetRequiredService<IDbContextFactory<DatabaseContext>>();
        await using var context = await factory.CreateDbContextAsync();

        using var scope = Fixture.Factory.RealServices.CreateScope();
        var archives = scope.ServiceProvider.GetRequiredService<ArchiveService>();

        var before = await archives.GetLibrarySizeAsync();

        var (first, _) = await SeedGameWithArchivesAsync(context, (100, new DateTime(2026, 1, 1)), (250, new DateTime(2026, 3, 1)), (175, new DateTime(2026, 2, 1)));
        var (second, _) = await SeedGameWithArchivesAsync(context, (40, new DateTime(2026, 1, 1)));

        try
        {
            Assert.Equal(before + 250 + 40, await archives.GetLibrarySizeAsync());
        }
        finally
        {
            await RemoveAsync(context, first);
            await RemoveAsync(context, second);
        }
    }

    [Fact]
    public void GamesPage_ListsTheLibrary()
    {
        var page = Render<LANCommander.Server.UI.Pages.Games.Index>();

        page.WaitForAssertion(() =>
        {
            Assert.Matches(@"^\d[\d,]* titles?", page.Find(".games-header .lc-page-header-subtitle").TextContent.Trim());

            var row = page.FindAll("tbody tr.rz-data-row").First(r => r.TextContent.Contains(BUnitServerFixture.TestGameTitle));

            // No keys, no archive, no cover: the gaps each column shows
            Assert.Contains("—", row.TextContent);
            Assert.Contains("none", row.QuerySelector(".games-danger")!.TextContent);
            Assert.Equal("missing-art", row.GetAttribute("data-lc-mark"));
            Assert.NotNull(row.QuerySelector(".games-icon-missing"));
            Assert.NotNull(row.QuerySelector(".games-kebab"));
        }, TimeSpan.FromSeconds(10));

        Assert.Matches(@"^\d[\d,]* results?$", page.Find(".lc-table-total").TextContent.Trim());
        Assert.Contains("Title", page.Find(".lc-table-sorted-value").TextContent);
        Assert.Equal("Search titles", page.Find(".lc-table-search input").GetAttribute("placeholder"));
    }

    [Fact]
    public void FilterRail_ListsWhatIsMissing()
    {
        var rail = Render<GameFilterRail>(p => p.Add(x => x.Filters, new GameFilters()));

        rail.WaitForAssertion(() =>
        {
            var section = rail.FindAll(".games-rail-section").First(s => s.TextContent.Contains("Missing"));
            var names = section.QuerySelectorAll(".games-rail-name").Select(n => n.TextContent.Trim()).ToList();

            Assert.Equal(GameFilters.MissingValues, names);

            // The seeded game has no media at all, so it lacks a cover
            var cover = section.QuerySelectorAll(".games-rail-facet").First(f => f.TextContent.Contains("Cover"));
            Assert.NotEqual("0", cover.QuerySelector(".games-rail-count")!.TextContent.Trim());
        }, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void FilterRail_TogglingAMissingEntry_AddsTheFacet()
    {
        GameFilters? changed = null;

        var rail = Render<GameFilterRail>(p => p
            .Add(x => x.Filters, new GameFilters())
            .Add(x => x.FiltersChanged, filters => changed = filters));

        var logo = rail.WaitForElement(".games-rail-section .games-rail-facet[aria-pressed='false']");

        rail.FindAll(".games-rail-facet").First(f => f.TextContent.Contains("Logo")).Click();

        Assert.NotNull(changed);
        Assert.True(changed!.Has(GameFacet.Missing, "Logo"));
        Assert.NotNull(logo);
    }
}
