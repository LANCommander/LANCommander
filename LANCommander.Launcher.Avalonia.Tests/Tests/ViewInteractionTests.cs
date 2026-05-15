using System;
using System.Collections.ObjectModel;
using System.IO;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
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
/// Renders a view, mutates ViewModel state to simulate a user action (or a service
/// callback firing), pumps the dispatcher, and screenshots the post-mutation frame.
/// Catches binding regressions that ViewLayoutTests' single-snapshot-per-state coverage
/// can miss — e.g. an [ObservableProperty] that no longer notifies, or a converter
/// that drops updates after the first render.
///
/// Each scenario emits its own baseline. First run saves the screenshot and fails;
/// inspect, accept, commit to Baselines/ and CI is green from then on.
/// </summary>
public class ViewInteractionTests
{
    private const int WindowWidth  = 1200;
    private const int WindowHeight = 800;

    private static readonly IServiceProvider _testServices = new ServiceCollection()
        .AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning))
        .BuildServiceProvider();

    private static void RenderInteractAndAssert(
        Control view,
        Action interact,
        string screenshotName)
    {
        var window = new Window
        {
            Width   = WindowWidth,
            Height  = WindowHeight,
            Content = view,
        };
        window.Show();

        // Initial layout pass so bindings settle before we mutate state.
        Dispatcher.UIThread.RunJobs();

        interact();

        // Drain queued binding/render jobs the mutation generated.
        Dispatcher.UIThread.RunJobs();

        var actualPath   = ScreenshotHelper.Capture(window, screenshotName);
        var baselinePath = ScreenshotHelper.GetBaselinePath(screenshotName);
        var diffPath     = ScreenshotHelper.GetDiffPath(screenshotName);

        var result = VisualComparer.Compare(actualPath, baselinePath, diffPath);

        if (!result.BaselineExists)
        {
            Directory.CreateDirectory(ScreenshotHelper.BaselinesDirectory);
            File.Copy(actualPath, baselinePath, overwrite: true);
        }

        Assert.True(result.Passed, result.Summary);
    }

    // -------------------------------------------------------------------------
    // GameActionBar transitions Installed → Running when game launches
    // -------------------------------------------------------------------------
    [AvaloniaFact]
    public void GameActionBar_transitions_to_Running_after_play()
    {
        var vm = new GameActionBarViewModel(_testServices)
        {
            Title       = "Half-Life",
            IsInstalled = true,
            IsInLibrary = true,
            PlayTime    = "14h 22m",
            LastPlayed  = "3 days ago",
        };
        var view = new GameActionBarView { DataContext = vm };

        RenderInteractAndAssert(
            view,
            interact: () =>
            {
                // Simulates the play command's effect — IsRunning flips after process starts.
                vm.IsRunning  = true;
                vm.LastPlayed = "Now";
            },
            screenshotName: "GameActionBar_Interaction_BecomesRunning");
    }

    // -------------------------------------------------------------------------
    // GameActionBar transitions Idle → Installing with progress message
    // -------------------------------------------------------------------------
    [AvaloniaFact]
    public void GameActionBar_transitions_to_Installing_with_progress()
    {
        var vm = new GameActionBarViewModel(_testServices)
        {
            Title       = "Portal",
            IsInLibrary = true,
            PlayTime    = "0h 0m",
            LastPlayed  = "Never",
        };
        var view = new GameActionBarView { DataContext = vm };

        RenderInteractAndAssert(
            view,
            interact: () =>
            {
                // Simulates InstallService.OnInstall firing into the action bar.
                vm.IsInstalling   = true;
                vm.StatusMessage  = "Downloading… 64%";
            },
            screenshotName: "GameActionBar_Interaction_BecomesInstalling");
    }

    // -------------------------------------------------------------------------
    // GamesListView re-renders when filtered down to one item
    // -------------------------------------------------------------------------
    [AvaloniaFact]
    public void GamesListView_rerenders_after_collection_filtered()
    {
        var vm = new GamesListViewModel(_testServices);
        vm.Games = new ObservableCollection<GameItemViewModel>
        {
            new() { Title = "Half-Life",        ReleasedOn = new DateTime(1998, 11, 19), HasCover = false },
            new() { Title = "Quake III Arena",  ReleasedOn = new DateTime(1999, 12, 2),  HasCover = false },
            new() { Title = "Doom",             ReleasedOn = new DateTime(1993, 12, 10), HasCover = false, InLibrary = true },
            new() { Title = "Counter-Strike",   ReleasedOn = new DateTime(2000, 11, 8),  HasCover = false, InLibrary = true },
        };
        var view = new GamesListView { DataContext = vm };

        RenderInteractAndAssert(
            view,
            interact: () =>
            {
                // Simulates a user typing "Doom" — only one item should remain on screen.
                vm.Games = new ObservableCollection<GameItemViewModel>
                {
                    new() { Title = "Doom", ReleasedOn = new DateTime(1993, 12, 10), HasCover = false, InLibrary = true },
                };
            },
            screenshotName: "GamesListView_Interaction_FilteredToOne");
    }

    // -------------------------------------------------------------------------
    // GamesListView transitions populated → empty state
    // -------------------------------------------------------------------------
    [AvaloniaFact]
    public void GamesListView_shows_empty_state_after_clear()
    {
        var vm = new GamesListViewModel(_testServices);
        vm.Games = new ObservableCollection<GameItemViewModel>
        {
            new() { Title = "Half-Life",   ReleasedOn = new DateTime(1998, 11, 19), HasCover = false },
            new() { Title = "Doom",        ReleasedOn = new DateTime(1993, 12, 10), HasCover = false },
        };
        var view = new GamesListView { DataContext = vm };

        RenderInteractAndAssert(
            view,
            interact: () =>
            {
                vm.Games = new ObservableCollection<GameItemViewModel>();
                vm.StatusMessage = "No games match your filters.";
            },
            screenshotName: "GamesListView_Interaction_EmptyAfterClear");
    }
}
