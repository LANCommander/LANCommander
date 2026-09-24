#if DEBUG
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using LANCommander.Launcher.ViewModels;
using LANCommander.Launcher.ViewModels.Components;
using LANCommander.Launcher.Views;

namespace LANCommander.Launcher.Fixtures.Sets;

/// <summary>The game page in each install and play state.</summary>
public static class GameDetailFixtures
{
    public static IReadOnlyList<ViewFixture> All { get; } =
    [
        new("GameDetail.NotInstalled", "A depot game that isn't in the library", context =>
        {
            var detail = Fill(context.Shell.DepotGameDetailViewModel, FixtureGames.NaturalSelection2);
            detail.ActionBar.DownloadSizeText = "9.8 GB";

            return context.ShellWindow(detail);
        }),

        new("GameDetail.InLibrary", "A library game that isn't installed", context =>
        {
            var detail = Fill(context.Shell.GameDetailViewModel, FixtureGames.Diablo2);
            detail.ActionBar.DownloadSizeText = "2.1 GB";

            return context.ShellWindow(detail);
        }),

        new("GameDetail.Installed", "An installed game with media, tools, notes and manuals", context =>
            context.ShellWindow(Installed(context))),

        new("GameDetail.Installed.Full", "The whole page of an installed game", context =>
            context.ShellWindow(Installed(context))) { Height = 1300 },

        new("GameDetail.MediaTab", "The media tab of an installed game", context =>
            context.ShellWindow(Installed(context)))
        {
            Prepare = (_, window) => window.GetVisualDescendants().OfType<GameDetailView>().Single()
                .FindControl<Button>("MediaTabButton")!
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent)),
        },

        new("GameDetail.UpdateAvailable", "An installed game with an update waiting", context =>
        {
            var detail = Fill(context.Shell.GameDetailViewModel, FixtureGames.AgeOfEmpires2);
            detail.ActionBar.IsUpdateAvailable = true;

            return context.ShellWindow(detail);
        }),

        new("GameDetail.Installing", "A game partway through installing", context =>
        {
            var detail = Fill(context.Shell.GameDetailViewModel, FixtureGames.Halo);
            detail.ActionBar.IsInstalling = true;
            detail.ActionBar.DownloadSizeText = "1.2 GB";

            return context.ShellWindow(detail);
        }),

        new("GameDetail.Running", "An installed game that is running", context =>
        {
            var detail = Fill(context.Shell.GameDetailViewModel, FixtureGames.Quake3);
            detail.ActionBar.IsRunning = true;
            detail.ActionBar.LastPlayed = Localize("LastPlayedJustNow");

            return context.ShellWindow(detail);
        }),

        new("GameDetail.Offline", "A game that can't be installed while offline", context =>
        {
            var shell = context.Shell;
            var detail = Fill(shell.GameDetailViewModel, FixtureGames.Diablo2);

            shell.SetOfflineMode(true);
            detail.IsOfflineMode = true;

            return context.ShellWindow(detail);
        }),

        new("GameDetail.NoArt", "A game the server has no media or metadata for", context =>
        {
            var detail = context.Shell.DepotGameDetailViewModel;
            var game = FixtureGames.Soldat;

            detail.Id = game.Id;
            detail.Title = game.Title;
            detail.ActionBar.GameId = game.Id;
            detail.ActionBar.Title = game.Title;
            detail.ActionBar.PlayTime = Localize("PlayStatNone");
            detail.ActionBar.LastPlayed = Localize("LastPlayedNever");

            return context.ShellWindow(detail);
        }),

        new("GameDetail.AllTags", "A game with a long tag list, expanded", context =>
        {
            var detail = Fill(context.Shell.GameDetailViewModel, FixtureGames.Battlefield1942);

            detail.Tags = string.Join(", ", detail.TagList.Concat(
                ["Sandbox", "Physics", "Historical", "Modding", "Dedicated Servers", "Classic Mods", "Squads", "Conquest", "Capture the Flag"]));
            detail.TagsExpanded = true;

            return context.ShellWindow(detail);
        }),
    ];

    private const string InstalledNotes =
        """
        ### LAN setup

        Everyone needs the **3369** patch. The server runs *ONS-Torlan* and *ONS-Primeval* on rotation.

        - Voice chat is off; use the party's own
        - Admin password is on the whiteboard
        - Report crashes to `#lan-help`
        """;

    /// <summary>
    /// Fills a game page the way loading it would, with the action bar in the state the game's
    /// catalog entry implies. Callers adjust the action bar afterwards for other states.
    /// </summary>
    internal static T Fill<T>(T detail, FixtureGame game) where T : GameDetailViewModel
    {
        detail.Id = game.Id;
        detail.Title = game.Title;
        detail.Description = game.Description;
        detail.CoverPath = game.CoverPath;
        detail.CoverMimeType = "image/png";
        detail.LogoPath = game.LogoPath;
        detail.BackgroundPath = game.BackgroundPath;
        detail.IconPath = game.IconPath;
        detail.ReleasedOn = game.ReleasedOn;
        detail.ReleaseYear = game.Year.ToString();
        detail.Genres = game.Genres;
        detail.Developers = game.Developers;
        detail.Publishers = game.Publishers;
        detail.Tags = game.Tags;
        detail.Platforms = "Windows";
        detail.Singleplayer = game.Singleplayer;
        detail.HasMultiplayer = game.MaxPlayers > 0;
        detail.PlayersText = string.Join(Environment.NewLine, game.PlayerModes());
        detail.FromLibrary = detail is not DepotGameDetailViewModel;

        var actionBar = detail.ActionBar;

        actionBar.GameId = game.Id;
        actionBar.Title = game.Title;
        actionBar.IsInLibrary = game.InLibrary;
        actionBar.IsInstalled = game.Installed;
        actionBar.IsUpdateAvailable = game.UpdateAvailable;

        if (game.Installed)
        {
            // Spelled out rather than Path.Combine, which would use '/' on Linux.
            actionBar.InstallDirectory = $@"C:\Games\{game.Title}";
            actionBar.InstalledVersion = "1.2.0";
            actionBar.PlayTime = Localize("PlayTimeHours", "14.5");
            actionBar.LastPlayed = Localize("LastPlayedDaysAgo", 3);

            // After the install folder, which starts a measurement of the real one if it exists.
            detail.SetSizeOnDiskForFixture("4.2 GB");
        }
        else
        {
            actionBar.PlayTime = Localize("PlayStatNone");
            actionBar.LastPlayed = Localize("LastPlayedNever");
        }

        return detail;
    }

    /// <summary>Unreal Tournament 2004 with everything a page can show.</summary>
    internal static GameDetailViewModel Installed(FixtureContext context)
    {
        var game = FixtureGames.UnrealTournament2004;
        var detail = Fill(context.Shell.GameDetailViewModel, game);
        var actionBar = detail.ActionBar;

        detail.Notes = InstalledNotes;

        detail.SeedFixture(
            Enumerable.Range(0, 6).Select(i => Screenshot(game.Title, i)),
            [
                new ToolItemViewModel(new SDK.Models.Tool { Id = FixtureGames.IdFor("UT2004 Dedicated Server"), Name = "UT2004 Dedicated Server" }, isInstalled: true),
                new ToolItemViewModel(new SDK.Models.Tool { Id = FixtureGames.IdFor("UnrealEd 3"), Name = "UnrealEd 3" }, isInstalled: false),
                new ToolItemViewModel(new SDK.Models.Tool { Id = FixtureGames.IdFor("Map Pack Installer"), Name = "Map Pack Installer" }, isInstalled: false),
            ]);

        actionBar.Actions = new(Actions("Play", "Play (Windowed)"));
        actionBar.HasMultipleActions = true;
        actionBar.SecondaryActions = new(Actions("Dedicated Server", "UnrealEd"));
        actionBar.HasSecondaryActions = true;
        actionBar.Manuals = new([new ManualViewModel("Game Manual", "manual.pdf", _ => { })]);
        actionBar.HasManuals = true;

        return detail;
    }

    internal static IEnumerable<GameActionViewModel> Actions(params string[] names) =>
        names.Select(name => new GameActionViewModel(
            new SDK.Models.Manifest.Action { Name = name, Path = "System/UT2004.exe" },
            _ => Task.CompletedTask));

    private static GameMediaItemViewModel Screenshot(string title, int index)
    {
        var path = FixtureArt.Screenshot(title, index);

        // Tiles show a decoded bitmap, not the path; the page decodes at 384 DIP.
        using var stream = File.OpenRead(path);

        return new GameMediaItemViewModel
        {
            Path = path,
            Name = $"Screenshot {index + 1}",
            MimeType = "image/png",
            ImageSource = Bitmap.DecodeToWidth(stream, 768),
        };
    }
}
#endif
