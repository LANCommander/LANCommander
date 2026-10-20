#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using LANCommander.Launcher.Settings.Enums;
using LANCommander.Launcher.ViewModels;
using LANCommander.Launcher.ViewModels.Components;
using LANCommander.SDK.Enums;

namespace LANCommander.Launcher.Fixtures.Sets;

/// <summary>The depot home page and its browse page.</summary>
public static class DepotFixtures
{
    public static IReadOnlyList<ViewFixture> All { get; } =
    [
        new("Depot.Home", "Depot home with every section filled", context =>
        {
            FillHome(context.Shell.DepotViewModel);

            return context.ShellWindow(context.Shell.DepotViewModel);
        }),

        new("Depot.Home.Full", "The whole depot home, down to the last carousel", context =>
        {
            FillHome(context.Shell.DepotViewModel);

            return context.ShellWindow(context.Shell.DepotViewModel);
        }) { Height = 1800 },

        new("Depot.Home.AllGenres", "Depot home with more genres than fit, expanded", context =>
        {
            var depot = context.Shell.DepotViewModel;

            FillHome(depot);

            foreach (var (name, count) in ExtraGenres)
                depot.GenreChips.Add(new DepotGenreChip(name, count));

            depot.GenresExpanded = true;

            return context.ShellWindow(depot);
        }),

        new("Depot.Home.Offline", "Depot home while offline: only what's cached locally", context =>
        {
            var shell = context.Shell;
            var depot = shell.DepotViewModel;
            var cached = FixtureGames.Library.Where(g => g.Type == GameType.MainGame).Select(g => g.ToItem(libraryBadge: false)).ToList();

            shell.SetOfflineMode(true);
            depot.IsOfflineMode = true;
            depot.IsLoading = false;

            // Offline, the depot shows the same cached games in both rails.
            foreach (var game in cached)
            {
                depot.PopularGames.Add(game);
                depot.BacklogGames.Add(game);
            }

            depot.HasPopularGames = true;
            depot.HasBacklogGames = true;
            depot.HasContent = true;
            depot.TotalTitles = cached.Count;
            depot.BacklogTitles = cached.Count;

            return context.ShellWindow(depot);
        }),

        new("Depot.Home.Loading", "Depot home while the catalog loads", context =>
            context.ShellWindow(context.Shell.DepotViewModel)),

        new("Depot.Home.Empty", "Depot home when the server has no games", context =>
        {
            context.Shell.DepotViewModel.IsLoading = false;

            return context.ShellWindow(context.Shell.DepotViewModel);
        }),

        new("Depot.Home.Error", "Depot home when the catalog failed to load", context =>
        {
            var depot = context.Shell.DepotViewModel;

            depot.IsLoading = false;
            depot.HasError = true;
            depot.StatusMessage = "Failed to load depot: The server at lancommander.lan:1337 did not respond.";

            return context.ShellWindow(depot);
        }),

        Browse("Depot.Browse", "All games in the cover grid", GameViewType.Grid),
        Browse("Depot.Browse.List", "All games as a list", GameViewType.List),
        Browse("Depot.Browse.Shelf", "All games as shelves, grouped by first letter", GameViewType.Horizontal),
        Browse("Depot.Browse.GroupedByGenre", "The list grouped by genre", GameViewType.List,
            configure: browse => browse.SelectedGroupBy = GroupBy.Genre),
        Browse("Depot.Browse.Genre", "Browsing one genre, locked in the rail", GameViewType.Grid, genre: "Strategy"),
        Browse("Depot.Browse.PlayTogether", "The Play Together preset: multiplayer games only", GameViewType.Grid,
            preset: DepotBrowsePreset.PlayTogether),
        Browse("Depot.Browse.Backlog", "The Backlog preset, filtered to the library", GameViewType.Grid,
            preset: DepotBrowsePreset.Backlog),
        Browse("Depot.Browse.Filters", "The grid with the advanced filters open", GameViewType.Grid,
            configure: browse => browse.IsAdvancedFilterOpen = true),
        Browse("Depot.Browse.NoResults", "A browse whose filters match nothing", GameViewType.Grid,
            configure: browse =>
            {
                browse.SelectedGenre = browse.AvailableGenres.First(g => g.Name == "Role-Playing");
                browse.SelectedMinPlayers = "32+";
            }),
    ];

    /// <summary>Genres beyond the catalog's own, for the "See all" state. Counts are plausible, not derived.</summary>
    private static readonly (string Name, int Count)[] ExtraGenres =
    [
        ("Adventure", 6), ("Racing", 5), ("Simulation", 4), ("Sports", 4), ("Puzzle", 3), ("Fighting", 2),
    ];

    private static IEnumerable<FixtureGame> MainGames => FixtureGames.All.Where(g => g.Type == GameType.MainGame);

    private static bool IsMultiplayer(FixtureGame game) => game.LocalPlayers > 0 || game.LanPlayers > 0 || game.OnlinePlayers > 0;

    private static void FillHome(DepotViewModel depot)
    {
        var games = MainGames.ToList();

        depot.IsLoading = false;

        foreach (var game in games.OrderByDescending(g => g.Year).Take(6))
            depot.PopularGames.Add(game.ToItem());

        foreach (var game in games.OrderByDescending(g => g.ReleasedOn))
            depot.NewReleases.Add(game.ToItem());

        var collections = games
            .SelectMany(g => g.Collections.Split(", ", StringSplitOptions.RemoveEmptyEntries), (game, collection) => (game, collection))
            .GroupBy(x => x.collection)
            .OrderBy(g => g.Key, StringComparer.Ordinal);

        foreach (var collection in collections)
            depot.BrowseCollections.Add(new GenreCarouselButtomViewModel(
                new SDK.Models.Genre { Name = collection.Key }, collection.First().game.BackgroundPath));

        foreach (var game in games.Where(g => g.InLibrary && !g.Installed))
            depot.BacklogGames.Add(game.ToItem(libraryBadge: false));

        foreach (var game in games.Where(IsMultiplayer).OrderBy(g => g.Title, StringComparer.Ordinal))
            depot.MultiplayerGames.Add(game.ToItem());

        var genres = games
            .SelectMany(g => g.Genres.Split(", ", StringSplitOptions.RemoveEmptyEntries))
            .GroupBy(g => g)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal);

        foreach (var genre in genres)
            depot.GenreChips.Add(new DepotGenreChip(genre.Key, genre.Count()));

        depot.HasPopularGames = depot.PopularGames.Count > 0;
        depot.HasNewReleases = depot.NewReleases.Count > 0;
        depot.HasBrowseCollections = depot.BrowseCollections.Count > 0;
        depot.HasBacklogGames = depot.BacklogGames.Count > 0;
        depot.HasMultiplayerGames = depot.MultiplayerGames.Count > 0;
        depot.HasBrowseGenres = depot.GenreChips.Count > 0;
        depot.HasBrowseData = true;
        depot.HasContent = true;

        SetCounts(depot);
    }

    /// <summary>The rail counts, which the browse page shows too.</summary>
    private static void SetCounts(DepotViewModel depot)
    {
        depot.TotalTitles = MainGames.Count();
        depot.MultiplayerTitles = MainGames.Count(IsMultiplayer);
        depot.BacklogTitles = MainGames.Count(g => g.InLibrary && !g.Installed);
    }

    private static ViewFixture Browse(
        string name,
        string description,
        GameViewType viewType,
        string? genre = null,
        DepotBrowsePreset preset = DepotBrowsePreset.None,
        Action<DepotBrowseViewModel>? configure = null) =>
        new(name, description, context =>
        {
            var shell = context.Shell;
            var browse = shell.DepotBrowseViewModel;

            SetCounts(shell.DepotViewModel);

            // Initialize picks the grouping from the view type, so the view type goes first.
            browse.SelectedViewType = viewType;
            browse.Initialize(FixtureGames.All.Select(g => g.ToItem()), preFilterGenre: genre, preset: preset);

            configure?.Invoke(browse);

            return context.ShellWindow(browse);
        });
}
#endif
