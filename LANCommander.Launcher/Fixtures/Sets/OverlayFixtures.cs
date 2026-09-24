#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using LANCommander.Launcher.ViewModels;
using LANCommander.Launcher.ViewModels.Components;
using LANCommander.Launcher.Views;

namespace LANCommander.Launcher.Fixtures.Sets;

/// <summary>Dialogs the game page opens over the main window.</summary>
public static class OverlayFixtures
{
    private const long MB = GameActionBarViewModel.MB;

    public static IReadOnlyList<ViewFixture> All { get; } =
    [
        Over("Overlay.InstallOptions", "Installing a game with expansions, mods and tools to choose from",
            () => new InstallOptionsOverlay { DataContext = InstallOptions(withAddons: true) }),

        Over("Overlay.InstallOptions.DirectoryOnly", "Installing a game with nothing to choose but the folder",
            () => new InstallOptionsOverlay { DataContext = InstallOptions(withAddons: false) }),

        Over("Overlay.Manage.Options", "Game options, with every kind of field", () => Manage(0)),
        Over("Overlay.Manage.Modify", "Changing which add-ons an install has", () => Manage(1)),
        Over("Overlay.Manage.Versions", "Switching between versions of a game", () => Manage(2)),
        Over("Overlay.Manage.Saves", "Cloud saves for a game", () => Manage(3)),

        Over("Overlay.Alert", "An error reported to the user",
            () => new AlertOverlay("Failed to Launch",
                "The game could not be started because UT2004.exe is missing from C:\\Games\\Unreal Tournament 2004\\System. " +
                "Verify the game's files or reinstall it.")),

        Over("Overlay.GameActions", "Choosing between a game's play actions",
            () => new GameActionsOverlay
            {
                DataContext = new GameActionsOverlayViewModel(FixtureGames.UnrealTournament2004.Title,
                    GameDetailFixtures.Actions("Play", "Play (Windowed)", "Play (Safe Mode)", "Instant Action: Onslaught")),
            }),

        new("Overlay.Lightbox", "Screenshots open full screen, third of six",
            context => context.ShellWindow(GameDetailFixtures.Installed(context)))
        {
            Prepare = (_, window) =>
            {
                var lightbox = new LightboxOverlay();
                var title = FixtureGames.UnrealTournament2004.Title;

                AddToOverlayLayer(window, lightbox);

                // Show reads resources, so it has to wait until the overlay is in the tree.
                lightbox.Show(
                    Enumerable.Range(0, 6).Select(i => new LightboxItem
                    {
                        Type = LightboxItemType.Image,
                        Path = FixtureArt.Screenshot(title, i),
                        Title = $"Onslaught on Torlan, part {i + 1}",
                    }).ToList(),
                    startIndex: 2,
                    videoStartTimeMs: 0,
                    title: title);
            },
        },
    ];

    /// <summary>
    /// An overlay over the installed game's page, added the way the app adds them: to the main
    /// window's overlay layer, sized to it.
    /// </summary>
    private static ViewFixture Over(string name, string description, Func<Control> overlay) =>
        new(name, description, context => context.ShellWindow(GameDetailFixtures.Installed(context)))
        {
            Prepare = (_, window) => AddToOverlayLayer(window, overlay()),
        };

    internal static void AddToOverlayLayer(Window window, Control overlay)
    {
        var layer = OverlayLayer.GetOverlayLayer(window)
            ?? throw new InvalidOperationException("The window has no overlay layer.");

        overlay.HorizontalAlignment = HorizontalAlignment.Stretch;
        overlay.VerticalAlignment = VerticalAlignment.Stretch;
        overlay.Bind(Layoutable.WidthProperty, new Binding("Bounds.Width") { Source = layer });
        overlay.Bind(Layoutable.HeightProperty, new Binding("Bounds.Height") { Source = layer });

        layer.Children.Add(overlay);
    }

    /// <summary>
    /// Filled in the order the body expects: it only watches add-ons and tools that exist when its
    /// data context is set.
    /// </summary>
    private static InstallOptionsViewModel InstallOptions(bool withAddons, bool modify = false)
    {
        var options = new InstallOptionsViewModel
        {
            GameTitle = FixtureGames.Battlefield1942.Title,
            ConfirmButtonText = modify ? "Apply" : "Install",
            BaseDownloadSize = 1_180 * MB,
            BaseSpaceRequired = 1_650 * MB,
            IsModify = modify,
            AlwaysShowDirectory = modify,
        };

        options.DialogTitle = $"Install {options.GameTitle}";

        options.InstallDirectories.Add(@"C:\Games");
        options.InstallDirectories.Add(@"D:\Games");
        options.SelectedInstallDirectory = options.InstallDirectories[modify ? 1 : 0];

        if (withAddons)
        {
            foreach (var addon in GameActionBarViewModel.FixtureAddons)
            {
                options.Addons.Add(new InstallAddonItemViewModel(new SDK.Models.Game
                {
                    Id = FixtureGames.IdFor(addon.Title),
                    Title = addon.Title,
                    Type = addon.Type,
                    Archives = [new SDK.Models.Archive { CompressedSize = addon.DownloadMb * MB, UncompressedSize = addon.DownloadMb * MB * 3 / 2 }],
                }, addon.Selected || modify && addon.Title.StartsWith("Desert Combat", StringComparison.Ordinal)));
            }

            foreach (var tool in GameActionBarViewModel.FixtureTools)
            {
                options.Tools.Add(new InstallToolItemViewModel(new SDK.Models.Tool
                {
                    Id = FixtureGames.IdFor(tool.Name),
                    Name = tool.Name,
                    Archives = [new SDK.Models.Archive { CompressedSize = tool.DownloadMb * MB, UncompressedSize = tool.DownloadMb * MB * 2 }],
                }));
            }
        }

        return options;
    }

    private const string OptionSchema =
        """
        Options:
          Video:
            DisplayName: Video
            Description: Resolution and display settings
            Options:
              fullscreen:
                Type: bool
                DisplayName: Fullscreen
                Description: Start the game in fullscreen.
                Default: "true"
              resolution:
                Type: choice
                DisplayName: Resolution
                Default: 1920x1080
                Choices:
                  - Value: 1280x720
                  - Value: 1920x1080
                  - Value: 2560x1440
              fov:
                Type: int
                DisplayName: Field of View
                Description: Horizontal field of view in degrees.
                Default: "90"
          Network:
            DisplayName: Network
            Options:
              playername:
                Type: string
                DisplayName: Player Name
                Default: Player
              favorites:
                Type: list
                DisplayName: Favorite Servers
                Description: Shown first in the server browser.
                ItemType: string
                MaxItems: 8
        """;

    /// <summary>Saved values are a flat string map; a list is stored as its own JSON array.</summary>
    private const string OptionValues =
        """{ "Network.playername": "Pat", "Network.favorites": "[\"192.168.1.20:7777\",\"192.168.1.21:7777\"]" }""";

    /// <summary>The Manage dialog for the installed game, on the section at <paramref name="section"/>.</summary>
    private static ManageOverlay Manage(int section)
    {
        var title = FixtureGames.UnrealTournament2004.Title;

        var versions = new GameVersionsViewModel();

        versions.Versions.Add(Version("3369", "Final patch: fixes Onslaught vehicle desync and adds the bonus pack maps.", 2_900, new DateTime(2005, 11, 7), installed: false, newer: true));
        versions.Versions.Add(Version("3339", "Server browser fixes.", 2_870, new DateTime(2005, 4, 21), installed: true, newer: false));
        versions.Versions.Add(Version("3204", null, 2_810, new DateTime(2004, 9, 1), installed: false, newer: false));

        var saves = new GameSavesViewModel();

        saves.SetSaves(
        [
            Save(FixtureContext.Now.AddHours(-5), 1_842_000),
            Save(FixtureContext.Now.AddDays(-3), 1_790_000),
            Save(FixtureContext.Now.AddDays(-12), 1_655_000),
        ]);

        var manage = new ManageOverlayViewModel { DialogTitle = $"Manage {title}" };

        manage.Sections.Add(new ManageSectionViewModel
        {
            Title = "Options", IconValue = "SlidersHorizontal", ActionText = "Save",
            Content = GameOptionsOverlayViewModel.Build(OptionSchema, OptionValues, title)!,
        });
        manage.Sections.Add(new ManageSectionViewModel
        {
            Title = "Modify", IconValue = "Wrench", ActionText = "Apply",
            Content = InstallOptions(withAddons: true, modify: true),
        });
        manage.Sections.Add(new ManageSectionViewModel
        {
            Title = "Versions", IconValue = "ClockCounterClockwise",
            Content = versions,
        });
        manage.Sections.Add(new ManageSectionViewModel
        {
            Title = "Saves", IconValue = "FloppyDisk", ActionText = "Upload Current Save",
            Content = saves,
        });

        manage.SelectedSection = manage.Sections[section];

        return new ManageOverlay { DataContext = manage };
    }

    private static GameVersionItemViewModel Version(string version, string? changelog, long sizeMb, DateTime createdOn, bool installed, bool newer) =>
        new(new SDK.Models.GameVersion
        {
            Id = FixtureGames.IdFor("version " + version),
            Version = version,
            Changelog = changelog,
            CompressedSize = sizeMb * MB,
            ArchiveId = FixtureGames.IdFor("archive " + version),
            CreatedOn = DateTime.SpecifyKind(createdOn, DateTimeKind.Local),
        }, installed, newer);

    private static GameSaveItemViewModel Save(DateTime createdOn, long size) =>
        new(new SDK.Models.GameSave
        {
            Id = FixtureGames.IdFor("save " + createdOn.Ticks),
            CreatedOn = createdOn,
            Size = size,
        });
}
#endif
