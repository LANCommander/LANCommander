using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LANCommander.Launcher.Fixtures;
using LANCommander.Launcher.Views;
using Xunit;
using Xunit.Abstractions;

namespace LANCommander.Launcher.Tests.Tests;

/// <summary>Pages the shell navigates away from must leave the visual tree and be collectable.</summary>
public class ShellViewLifetimeTests(ITestOutputHelper output)
{
    [AvaloniaFact]
    public void SwitchingGamesLeavesOnePageAndReleasesTheRest()
    {
        using var context = FixtureContext.Create();

        var window = FixtureHost.Show(FixtureCatalog.Get("Library.List"), context);
        var seen = new List<WeakReference>();

        try
        {
            var shell = context.Shell;

            shell.ShowLibraryCommand.Execute(null);
            Settle();
            Track(window, seen);

            foreach (var game in FixtureGames.Library.Take(6))
            {
                shell.OnGameSelected(this, new SDK.Models.Game { Id = game.Id, Title = game.Title });
                Settle();
                Track(window, seen);

                Report(window, $"on {game.Title}");
                Assert.Equal(1, Attached<GameDetailView>(window));
                Assert.Equal(0, Attached<GamesListView>(window));
            }

            shell.ShowLibraryCommand.Execute(null);
            Settle();
            Track(window, seen);

            Report(window, "back on the library");
            Assert.Equal(0, Attached<GameDetailView>(window));
            Assert.Equal(1, Attached<GamesListView>(window));
        }
        finally
        {
            window.Close();
        }

        for (var i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        var alive = seen.Where(w => w.IsAlive).GroupBy(w => w.Target!.GetType().Name)
            .ToDictionary(g => g.Key, g => g.Count());

        output.WriteLine($"Pages created: {seen.Count}; still alive after the window closed: " +
            string.Join(", ", alive.Select(a => $"{a.Key}={a.Value}")));

        // Every game page goes. The library view model is still alive here (the fixture context is),
        // and its collections reach the last library page shown; earlier ones must not survive.
        Assert.False(alive.ContainsKey(nameof(GameDetailView)));
        Assert.True(alive.GetValueOrDefault(nameof(GamesListView)) <= 1);
    }

    [AvaloniaFact]
    public void ReturningToTheLibraryDoesNotKeepOldLibraryPagesAlive()
    {
        using var context = FixtureContext.Create();

        var window = FixtureHost.Show(FixtureCatalog.Get("Library.List"), context);
        var seen = new List<WeakReference>();

        try
        {
            var shell = context.Shell;
            var game = FixtureGames.Library.First();

            for (var i = 0; i < 5; i++)
            {
                shell.ShowLibraryCommand.Execute(null);
                Settle();
                Track(window, seen);

                shell.OnGameSelected(this, new SDK.Models.Game { Id = game.Id, Title = game.Title });
                Settle();
            }

            shell.ShowLibraryCommand.Execute(null);
            Settle();
            Track(window, seen);

            for (var i = 0; i < 3; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            var alive = seen.Count(w => w.IsAlive && w.Target is GamesListView);
            output.WriteLine($"Library pages created: {seen.Count(w => w.Target is GamesListView or null)}; alive with the window open: {alive}");

            // Only the one on screen
            Assert.Equal(1, alive);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void GamePagesReleaseTheirScreenshotsOnceNothingCanReturnToThem()
    {
        using var context = FixtureContext.Create();

        var window = FixtureHost.Show(FixtureCatalog.Get("Library.List"), context);

        try
        {
            var shell = context.Shell;
            var navigation = context.Get<LANCommander.Launcher.Services.INavigationService>();
            var games = FixtureGames.Library.Take(2).ToList();

            shell.ShowLibraryCommand.Execute(null);
            Settle();

            shell.OnGameSelected(this, new SDK.Models.Game { Id = games[0].Id, Title = games[0].Title });
            Settle();

            var first = (LANCommander.Launcher.ViewModels.GameDetailViewModel)shell.ContentView!;
            var screenshot = WithScreenshot(first);

            // Left for Settings, Back can still return to it: it keeps its screenshots
            navigation.NavigateTo(shell.SettingsViewModel);
            Settle();

            Assert.Same(screenshot, first.MediaItems.Single());
            Assert.NotNull(screenshot.ImageSource);

            navigation.GoBack();
            Settle();
            Assert.Same(first, shell.ContentView);

            // Replaced by another game from the rail: nothing can return to it
            shell.OnGameSelected(this, new SDK.Models.Game { Id = games[1].Id, Title = games[1].Title });
            Settle();

            Assert.NotSame(first, shell.ContentView);
            Assert.Empty(first.MediaItems);
            Assert.Null(screenshot.ImageSource);

            // Back to the library: the page that was showing lets go too
            var second = (LANCommander.Launcher.ViewModels.GameDetailViewModel)shell.ContentView!;
            var secondScreenshot = WithScreenshot(second);

            navigation.GoBack();
            Settle();

            Assert.Same(shell.LibraryViewModel, shell.ContentView);
            Assert.Empty(second.MediaItems);
            Assert.Null(secondScreenshot.ImageSource);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void LeavingAGamePageStopsItsCarouselVideos()
    {
        using var context = FixtureContext.Create();

        var window = FixtureHost.Show(FixtureCatalog.Get("Library.List"), context);

        try
        {
            var shell = context.Shell;
            var games = FixtureGames.Library.Take(2).ToList();

            shell.ShowLibraryCommand.Execute(null);
            Settle();

            shell.OnGameSelected(this, new SDK.Models.Game { Id = games[0].Id, Title = games[0].Title });
            Settle();

            var page = (LANCommander.Launcher.ViewModels.GameDetailViewModel)shell.ContentView!;

            // A local path that doesn't exist: LibVLC still creates its player for it
            page.SeedFixture(
                [new LANCommander.Launcher.ViewModels.Components.GameMediaItemViewModel
                {
                    IsVideo = true,
                    Path = @"C:\nonexistent\trailer.mp4",
                }],
                []);
            Settle();

            var players = window.GetVisualDescendants().OfType<LANCommander.Launcher.Controls.InlineVideoPlayer>().ToList();
            Assert.NotEmpty(players);

            var playing = players.Count(p => p.HasRenderer);
            output.WriteLine($"Video players on the page: {players.Count}; with a LibVLC renderer: {playing}");

            // Switch games from the rail
            shell.OnGameSelected(this, new SDK.Models.Game { Id = games[1].Id, Title = games[1].Title });
            Settle();

            Assert.Empty(window.GetVisualDescendants().OfType<LANCommander.Launcher.Controls.InlineVideoPlayer>());
            Assert.All(players, p => Assert.False(p.HasRenderer));
            Assert.Empty(page.MediaItems);
        }
        finally
        {
            window.Close();
        }
    }

    private static LANCommander.Launcher.ViewModels.Components.GameMediaItemViewModel WithScreenshot(
        LANCommander.Launcher.ViewModels.GameDetailViewModel page)
    {
        var item = new LANCommander.Launcher.ViewModels.Components.GameMediaItemViewModel
        {
            ImageSource = new Avalonia.Media.Imaging.WriteableBitmap(
                new PixelSize(16, 9), new Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Premul),
        };

        page.MediaItems.Clear();
        page.MediaItems.Add(item);

        return item;
    }

    private void Report(Window window, string when) =>
        output.WriteLine($"{when}: GameDetailView={Attached<GameDetailView>(window)}, " +
            $"GamesListView={Attached<GamesListView>(window)}, LibraryRowView={Attached<LibraryRowView>(window)}");

    private static int Attached<T>(Window window) where T : Visual =>
        window.GetVisualDescendants().OfType<T>().Count();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Track(Window window, List<WeakReference> seen)
    {
        foreach (var page in window.GetVisualDescendants().Where(v => v is GameDetailView or GamesListView))
            if (!seen.Any(w => ReferenceEquals(w.Target, page)))
                seen.Add(new WeakReference(page));
    }

    // Navigation yields to the dispatcher, and the page transition runs on the (real-time) render clock
    private static void Settle()
    {
        for (var i = 0; i < 5; i++)
            Dispatcher.UIThread.RunJobs();

        Thread.Sleep(300);

        for (var i = 0; i < 5; i++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }
    }
}
