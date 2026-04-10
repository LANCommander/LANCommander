using System;
using System.Collections.ObjectModel;
using System.IO;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using LANCommander.Launcher.Avalonia.Tests.Helpers;
using LANCommander.Launcher.Avalonia.ViewModels;
using LANCommander.Launcher.Avalonia.ViewModels.Components;
using LANCommander.Launcher.Avalonia.Views;
using LANCommander.Launcher.Avalonia.Views.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace LANCommander.Launcher.Avalonia.Tests.Tests;

/// <summary>
/// Renders each major view in the headless Avalonia environment and compares the
/// resulting screenshot against a committed baseline PNG.  A test fails when more
/// than <see cref="VisualComparer.DefaultMaxMismatchPercent"/> percent of pixels
/// differ from the baseline, indicating a visual regression.
///
/// FIRST RUN / NEW VIEWS
/// If no baseline PNG exists, the test saves the current screenshot and fails with
/// an informational message.  Inspect the screenshot in the CI artifact, then commit
/// it to Baselines/ to establish the baseline for future runs.  Alternatively, run
/// the "Update Visual Baselines" workflow dispatch to commit all baselines at once.
/// </summary>
public class ViewLayoutTests
{
    private const int WindowWidth  = 1200;
    private const int WindowHeight = 800;

    // ---------------------------------------------------------------------------
    // Service provider shared by all tests that need ViewModels with DI dependencies.
    // Minimal: just logging — no real SDK services needed for layout-only rendering.
    // ---------------------------------------------------------------------------
    private static readonly IServiceProvider _testServices = new ServiceCollection()
        .AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning))
        .BuildServiceProvider();

    // ---------------------------------------------------------------------------
    // Helper: wrap a view in a sized window, show it, capture and compare.
    // ---------------------------------------------------------------------------
    private static void CaptureAndAssert(Control view, string screenshotName)
    {
        var window = new Window
        {
            Width  = WindowWidth,
            Height = WindowHeight,
            Content = view,
        };
        window.Show();

        var actualPath   = ScreenshotHelper.Capture(window, screenshotName);
        var baselinePath = ScreenshotHelper.GetBaselinePath(screenshotName);
        var diffPath     = ScreenshotHelper.GetDiffPath(screenshotName);

        var result = VisualComparer.Compare(actualPath, baselinePath, diffPath);

        if (!result.BaselineExists)
        {
            // First run: copy the actual as the baseline so next run can compare.
            Directory.CreateDirectory(ScreenshotHelper.BaselinesDirectory);
            File.Copy(actualPath, baselinePath, overwrite: true);
        }

        Assert.True(result.Passed, result.Summary);
    }

    // ---------------------------------------------------------------------------
    // Login View
    // ---------------------------------------------------------------------------
    [AvaloniaFact]
    public void LoginView_Layout()
    {
        // LoginViewModel requires SDK services — for a pure layout test we render
        // the LoginView with a null DataContext and let the XAML show its skeleton.
        // The view's bindings gracefully no-op when DataContext is null.
        var view = new LoginView();
        CaptureAndAssert(view, "LoginView");
    }

    // ---------------------------------------------------------------------------
    // Server Selection View
    // ---------------------------------------------------------------------------
    [AvaloniaFact]
    public void ServerSelectionView_Layout()
    {
        var view = new ServerSelectionView();
        CaptureAndAssert(view, "ServerSelectionView");
    }

    // ---------------------------------------------------------------------------
    // Splash / Loading View
    // ---------------------------------------------------------------------------
    [AvaloniaFact]
    public void SplashView_Layout()
    {
        var view = new SplashView();
        CaptureAndAssert(view, "SplashView");
    }

    // ---------------------------------------------------------------------------
    // Games List (Depot) — populated with representative mock data
    // ---------------------------------------------------------------------------
    [AvaloniaFact]
    public void GamesListView_Layout()
    {
        var vm = new GamesListViewModel(_testServices);
        vm.Games = new ObservableCollection<GameItemViewModel>
        {
            new() { Title = "Half-Life",        ReleasedOn = new DateTime(1998, 11, 19), HasCover = false },
            new() { Title = "Quake III Arena",  ReleasedOn = new DateTime(1999, 12, 2),  HasCover = false },
            new() { Title = "Doom",             ReleasedOn = new DateTime(1993, 12, 10), HasCover = false, InLibrary = true },
            new() { Title = "Unreal Tournament",ReleasedOn = new DateTime(1999, 11, 16), HasCover = false },
            new() { Title = "Counter-Strike",   ReleasedOn = new DateTime(2000, 11, 8),  HasCover = false, InLibrary = true },
            new() { Title = "Team Fortress 2",  ReleasedOn = new DateTime(2007, 10, 10), HasCover = false },
            new() { Title = "Portal",           ReleasedOn = new DateTime(2007, 10, 10), HasCover = false, InLibrary = true },
            new() { Title = "Left 4 Dead",      ReleasedOn = new DateTime(2008, 11, 17), HasCover = false },
        };

        var view = new GamesListView { DataContext = vm };
        CaptureAndAssert(view, "GamesListView");
    }

    // ---------------------------------------------------------------------------
    // Games List (Depot) — empty state
    // ---------------------------------------------------------------------------
    [AvaloniaFact]
    public void GamesListView_EmptyState_Layout()
    {
        var vm = new GamesListViewModel(_testServices)
        {
            StatusMessage = "No games found matching your filters.",
        };

        var view = new GamesListView { DataContext = vm };
        CaptureAndAssert(view, "GamesListView_Empty");
    }

    // ---------------------------------------------------------------------------
    // Game Detail View — installed game with metadata
    // ---------------------------------------------------------------------------
    [AvaloniaFact]
    public void GameDetailView_Installed_Layout()
    {
        var vm = new GameDetailViewModel(_testServices)
        {
            Title        = "Half-Life",
            Description  = "You are Gordon Freeman, a theoretical physicist who must fight your way out of the Black Mesa Research Facility after a catastrophic accident opens a portal to an alien world.",
            Genres       = "Action, First-Person Shooter",
            Tags         = "Classic, Single-player, Sci-fi",
            Developers   = "Valve",
            Publishers   = "Sierra Studios",
            Singleplayer = true,
        };
        vm.ActionBar.Title       = "Half-Life";
        vm.ActionBar.IsInstalled = true;
        vm.ActionBar.IsInLibrary = true;
        vm.ActionBar.PlayTime    = "14h 22m";
        vm.ActionBar.LastPlayed  = "3 days ago";

        var view = new GameDetailView { DataContext = vm };
        CaptureAndAssert(view, "GameDetailView_Installed");
    }

    // ---------------------------------------------------------------------------
    // Game Detail View — not yet installed
    // ---------------------------------------------------------------------------
    [AvaloniaFact]
    public void GameDetailView_NotInstalled_Layout()
    {
        var vm = new GameDetailViewModel(_testServices)
        {
            Title       = "Quake III Arena",
            Description = "A pure multiplayer arena shooter featuring fast-paced frantic gameplay.",
            Genres      = "Action, First-Person Shooter",
            Developers  = "id Software",
            Publishers  = "Activision",
        };
        vm.ActionBar.Title        = "Quake III Arena";
        vm.ActionBar.IsInstalled  = false;
        vm.ActionBar.IsInLibrary  = true;
        // CanInstall is computed: !IsOfflineMode && !IsInstalled && !IsInstalling
        // With IsInstalled=false and IsOfflineMode=false (default), CanInstall will be true.
        vm.ActionBar.PlayTime     = "0h 0m";
        vm.ActionBar.LastPlayed   = "Never";

        var view = new GameDetailView { DataContext = vm };
        CaptureAndAssert(view, "GameDetailView_NotInstalled");
    }

    // ---------------------------------------------------------------------------
    // Game Action Bar — installing in progress
    // ---------------------------------------------------------------------------
    [AvaloniaFact]
    public void GameActionBar_Installing_Layout()
    {
        var vm = new GameActionBarViewModel(_testServices)
        {
            Title        = "Portal",
            IsInstalled  = false,
            IsInstalling = true,
            // CanInstall is computed: will be false because IsInstalling=true
            IsInLibrary  = true,
            PlayTime     = "0h 0m",
            LastPlayed   = "Never",
            StatusMessage = "Downloading… 42%",
        };

        var view = new GameActionBarView { DataContext = vm };
        CaptureAndAssert(view, "GameActionBar_Installing");
    }

    // ---------------------------------------------------------------------------
    // Game Action Bar — game running
    // ---------------------------------------------------------------------------
    [AvaloniaFact]
    public void GameActionBar_Running_Layout()
    {
        var vm = new GameActionBarViewModel(_testServices)
        {
            Title       = "Half-Life",
            IsInstalled = true,
            IsRunning   = true,
            IsInLibrary = true,
            PlayTime    = "14h 22m",
            LastPlayed  = "Now",
        };

        var view = new GameActionBarView { DataContext = vm };
        CaptureAndAssert(view, "GameActionBar_Running");
    }
}
