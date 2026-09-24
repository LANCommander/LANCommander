#if DEBUG
using System.Collections.Generic;
using Avalonia.Controls;
using LANCommander.Launcher.Views;
using LANCommander.Launcher.Views.Components;

namespace LANCommander.Launcher.Fixtures.Sets;

/// <summary>
/// Views that no page places at the moment. Rendered on their own so they don't rot unseen.
/// </summary>
public static class ComponentFixtures
{
    public static IReadOnlyList<ViewFixture> All { get; } =
    [
        new("Component.GameBanner", "A game banner with its background", _ => new GameBannerView
        {
            Title = FixtureGames.Warcraft3.Title,
            BannerPath = FixtureGames.Warcraft3.LogoPath,
            BackgroundPath = FixtureGames.Warcraft3.BackgroundPath,
        }) { Height = 300 },

        new("Component.GameBanner.Fallback", "A game banner with no banner art", _ => new GameBannerView
        {
            Title = FixtureGames.Warcraft3.Title,
            BannerPath = string.Empty,
            BackgroundPath = FixtureGames.Warcraft3.BackgroundPath,
        }) { Height = 300 },

        new("Component.PageHeader", "A page header with a back button", _ => new PageHeaderView
        {
            Title = "Unreal Tournament 2004",
            Subtitle = "Epic Games · 2004",
            BackButtonText = "Library",
            ShowBackButton = true,
        }) { Height = 160 },

        new("Component.DownloadQueue", "The compact download queue, expanded", context =>
        {
            var queue = context.Shell.DownloadQueue;

            DownloadFixtures.SeedQueue(queue, expanded: false);
            queue.IsExpanded = true;

            return new DownloadQueueView { DataContext = queue };
        }) { Width = 480, Height = 600 },
    ];
}
#endif
