#if DEBUG
using System.Collections.Generic;
using System.Linq;
using LANCommander.Launcher.Settings.Enums;
using LANCommander.Launcher.ViewModels;

namespace LANCommander.Launcher.Fixtures.Sets;

/// <summary>The library page (the shell's default) in each layout, grouping and empty/loading state.</summary>
public static class LibraryFixtures
{
    public static IReadOnlyList<ViewFixture> All { get; } =
    [
        Library("Library.Grid", "Library in the default cover grid", GameViewType.Grid),
        Library("Library.List", "Library in the list layout", GameViewType.List),
        Library("Library.Shelf", "Library in the horizontal shelf layout, grouped by letter", GameViewType.Horizontal),
        Library("Library.Grid.GroupedByGenre", "Cover grid grouped by genre", GameViewType.Grid, GroupBy.Genre),
        Library("Library.Grid.NoArt", "Cover grid for games with no media yet", GameViewType.Grid, withArt: false),
        Library("Library.Filters", "Cover grid with the advanced filter panel open", GameViewType.Grid,
            configure: library => library.IsAdvancedFilterOpen = true),
        Library("Library.Offline", "Library while the launcher is offline", GameViewType.Grid, offline: true),
        Library("Library.Syncing", "Library while an import is running", GameViewType.Grid,
            configureShell: shell =>
            {
                shell.IsImportRunning = true;
                shell.ImportTotal = 18;
                shell.ImportIndex = 7;
            }),

        new("Library.Empty", "Library with no games in it", context =>
        {
            var library = context.Shell.LibraryViewModel;
            library.SeedFixture([]);

            return context.ShellWindow(library);
        }),

        new("Library.Loading", "Library on first load, before anything is cached", context =>
        {
            var library = context.Shell.LibraryViewModel;
            library.SeedFixture([]);
            library.IsLoading = true;

            return context.ShellWindow(library);
        }),
    ];

    private static ViewFixture Library(
        string name,
        string description,
        GameViewType viewType,
        GroupBy groupBy = GroupBy.None,
        bool withArt = true,
        bool offline = false,
        System.Action<LibraryViewModel>? configure = null,
        System.Action<ShellViewModel>? configureShell = null) =>
        new(name, description, context =>
        {
            var shell = context.Shell;
            var library = shell.LibraryViewModel;

            if (offline)
            {
                shell.SetOfflineMode(true);
                library.IsOfflineMode = true;
            }

            library.SelectedViewType = viewType;

            // The shelf forces a grouping of its own; only override it when asked to.
            if (groupBy != GroupBy.None)
                library.SelectedGroupBy = groupBy;

            library.SeedFixture(FixtureGames.Library.Select(g => g.ToItem(withArt, libraryBadge: false)));

            configure?.Invoke(library);

            return context.ShellWindow(library, configureShell);
        });
}
#endif
