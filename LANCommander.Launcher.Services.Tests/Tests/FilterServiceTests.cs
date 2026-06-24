using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Models;
using LANCommander.Launcher.Services.Tests.Helpers;
using LANCommander.SDK.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace LANCommander.Launcher.Services.Tests.Tests;

public class FilterServiceTests
{
    private static FilterService CreateSubject()
    {
        // FilterService.Populate / FilterLibraryItems do not touch LibraryService, so we
        // pass null! — Moq can't easily mock a concrete class with deep dependencies and
        // we don't dereference it. SettingsProvider is real but backed by an in-memory
        // IOptionsMonitor so no file I/O occurs.
        var service = new FilterService(
            NullLogger<FilterService>.Instance,
            libraryService: null!,
            settingsProvider: TestSettingsProvider.Create())
        {
            Filter = new LibraryFilterModel(),
        };

        return service;
    }

    [Fact]
    public void Populate_extracts_distinct_metadata_across_games()
    {
        var actionGenre = new Genre { Id = Guid.NewGuid(), Name = "Action" };
        var fpsGenre    = new Genre { Id = Guid.NewGuid(), Name = "FPS" };
        var classicTag  = new Tag   { Id = Guid.NewGuid(), Name = "Classic" };
        var valve       = new Company { Id = Guid.NewGuid(), Name = "Valve" };
        var idSoftware  = new Company { Id = Guid.NewGuid(), Name = "id Software" };
        var goldsrc     = new Engine { Id = Guid.NewGuid(), Name = "GoldSrc" };

        var games = new[]
        {
            GameFactory.Make("Half-Life",
                engine: goldsrc,
                genres: [actionGenre, fpsGenre],
                tags: [classicTag],
                developers: [valve],
                publishers: [valve],
                singleplayer: true),

            GameFactory.Make("Quake III Arena",
                genres: [actionGenre],
                developers: [idSoftware],
                publishers: [idSoftware],
                multiplayerModes: [new MultiplayerMode { MinPlayers = 2, MaxPlayers = 16 }]),
        };

        var subject = CreateSubject();

        subject.Populate(games);

        subject.Genres.Select(g => g.Name).ShouldBe(["Action", "FPS"], ignoreOrder: false);
        subject.Tags.Single().Name.ShouldBe("Classic");
        subject.Developers.Select(c => c.Name).ShouldBe(["id Software", "Valve"]);
        subject.Engines.Single().Name.ShouldBe("GoldSrc");
        subject.MinPlayers.ShouldBe(1, customMessage: "singleplayer game forces MinPlayers=1");
        subject.MaxPlayers.ShouldBe(16);
    }

    [Fact]
    public void FilterLibraryItems_filters_by_title_substring_case_insensitive()
    {
        var subject = CreateSubject();
        subject.Filter.Title = "QUAKE";

        var items = new[]
        {
            GameFactory.Make("Half-Life").AsListItem(),
            GameFactory.Make("Quake III Arena").AsListItem(),
            GameFactory.Make("Doom").AsListItem(),
        };

        var filtered = subject.FilterLibraryItems(items).ToList();

        filtered.Single().Name.ShouldBe("Quake III Arena");
    }

    [Fact]
    public void FilterLibraryItems_filters_by_genre_intersection()
    {
        var fps = new Genre { Id = Guid.NewGuid(), Name = "FPS" };
        var rts = new Genre { Id = Guid.NewGuid(), Name = "RTS" };

        var subject = CreateSubject();
        subject.Filter.Genres = [fps];

        var items = new[]
        {
            GameFactory.Make("Half-Life", genres: [fps]).AsListItem(),
            GameFactory.Make("StarCraft", genres: [rts]).AsListItem(),
            GameFactory.Make("Counter-Strike", genres: [fps]).AsListItem(),
        };

        var filtered = subject.FilterLibraryItems(items).Select(i => i.Name).ToList();

        filtered.ShouldBe(["Half-Life", "Counter-Strike"], ignoreOrder: true);
    }

    [Fact]
    public void FilterLibraryItems_when_installed_only_returns_installed_games()
    {
        var subject = CreateSubject();
        subject.Filter.Installed = true;

        var items = new[]
        {
            GameFactory.Make("Half-Life",      installed: true).AsListItem(),
            GameFactory.Make("Quake III Arena", installed: false).AsListItem(),
        };

        subject.FilterLibraryItems(items).Single().Name.ShouldBe("Half-Life");
    }

    [Fact]
    public void FilterLibraryItems_excludes_non_main_game_types()
    {
        var subject = CreateSubject();

        var items = new[]
        {
            GameFactory.Make("Half-Life",   type: GameType.MainGame).AsListItem(),
            GameFactory.Make("Opposing Force", type: GameType.Expansion).AsListItem(),
            GameFactory.Make("Blue Shift",  type: GameType.StandaloneExpansion).AsListItem(),
        };

        var names = subject.FilterLibraryItems(items).Select(i => i.Name).ToList();

        names.ShouldContain("Half-Life");
        names.ShouldContain("Blue Shift");
        names.ShouldNotContain("Opposing Force");
    }
}
