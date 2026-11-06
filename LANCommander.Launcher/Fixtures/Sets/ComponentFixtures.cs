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

        new("Component.PresenceCard", "What another player is doing, as the card over their name shows it", _ =>
            PresenceCardHost(FixturePresence.Playing(FixtureGames.IdFor("user Sam"), "Sam", FixtureGames.UnrealTournament2004, 42,
                FixturePresence.InAMatch))) { Width = 380, Height = 140 },

        new("Component.PresenceCard.GameOnly", "A player in a game that reports nothing more, with no icon on the server", _ =>
            PresenceCardHost(FixturePresence.Playing(FixtureGames.IdFor("user Jordan"), "Jordan", FixtureGames.Quake3, 75,
                withIcon: false))) { Width = 380, Height = 120 },

        new("Component.DownloadQueue", "The compact download queue, expanded", context =>
        {
            var queue = context.Shell.DownloadQueue;

            DownloadFixtures.SeedQueue(queue, expanded: false);
            queue.IsExpanded = true;

            return new DownloadQueueView { DataContext = queue };
        }) { Width = 480, Height = 600 },
    ];

    // Drawn the way a tooltip frames it
    private static Control PresenceCardHost(ViewModels.Components.PlayerPresenceViewModel player) => new Border
    {
        Margin = new Avalonia.Thickness(16),
        Padding = new Avalonia.Thickness(10, 8),
        CornerRadius = new Avalonia.CornerRadius(6),
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
        [!Border.BackgroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("SurfaceRaisedBrush"),
        Child = new PresenceCard { DataContext = player },
    };
}
#endif
