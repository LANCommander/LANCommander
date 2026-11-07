using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LANCommander.Launcher.Fixtures;
using LANCommander.Launcher.Settings.Enums;
using LANCommander.Launcher.ViewModels;
using LANCommander.Launcher.Views.Components;
using Xunit;

namespace LANCommander.Launcher.Tests.Tests;

/// <summary>The shell's left rail, which stays beside the library, the depot and their game pages.</summary>
public class ShellRailTests
{
    [AvaloniaFact]
    public void LibraryButtonLeavesALibraryGamePage()
    {
        using var context = FixtureContext.Create();

        var window = FixtureHost.Show(FixtureCatalog.Get("Library.Grid"), context);

        try
        {
            // Opened the way the app opens it: through navigation, from the library
            var navigation = context.Get<LANCommander.Launcher.Services.INavigationService>();
            context.Shell.GameDetailViewModel.FromLibrary = true;
            context.Shell.ShowLibraryCommand.Execute(null);
            Pump();
            navigation.NavigateTo(context.Shell.GameDetailViewModel);
            Pump();

            Assert.Same(context.Shell.GameDetailViewModel, context.Shell.ContentView);

            Click(window, RailButton(window, "Library"));

            Assert.Same(context.Shell.LibraryViewModel, context.Shell.ContentView);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SwitchingGamesFromTheRailSwapsThePageAndBackStillReturnsToTheLibrary()
    {
        using var context = FixtureContext.Create();

        var window = FixtureHost.Show(FixtureCatalog.Get("Library.List"), context);

        try
        {
            var shell = context.Shell;
            var navigation = context.Get<LANCommander.Launcher.Services.INavigationService>();

            shell.ShowLibraryCommand.Execute(null);
            Pump();

            shell.OnGameSelected(this, Game(FixtureGames.Diablo2));
            Pump();

            var first = Assert.IsAssignableFrom<GameDetailViewModel>(shell.ContentView);
            Assert.Equal(FixtureGames.Diablo2.Id, first.Id);

            shell.OnGameSelected(this, Game(FixtureGames.Quake3));
            Pump();

            // A different page, so the content fades in; the outgoing one kept its own game
            var second = Assert.IsAssignableFrom<GameDetailViewModel>(shell.ContentView);
            Assert.NotSame(first, second);
            Assert.Equal(FixtureGames.Quake3.Id, second.Id);
            Assert.Equal(FixtureGames.Diablo2.Id, first.Id);
            Assert.Same(second, shell.GameDetailViewModel);
            Assert.True(shell.IsLibraryRail);

            // A third game reuses the first page
            shell.OnGameSelected(this, Game(FixtureGames.Halo));
            Pump();

            Assert.Same(first, shell.ContentView);
            Assert.Equal(FixtureGames.Halo.Id, first.Id);

            // Switching replaced the page rather than stacking it
            navigation.GoBack();
            Pump();

            Assert.Same(shell.LibraryViewModel, shell.ContentView);
        }
        finally
        {
            window.Close();
        }
    }

    private static SDK.Models.Game Game(FixtureGame game) => new() { Id = game.Id, Title = game.Title };

    [AvaloniaTheory]
    [InlineData(GameViewType.Grid)]
    [InlineData(GameViewType.List)]
    [InlineData(GameViewType.Horizontal)]
    public void LibraryHasTheRailInEveryLayout(GameViewType viewType)
    {
        using var context = FixtureContext.Create();

        var shell = context.Shell;
        shell.LibraryViewModel.SelectedViewType = viewType;
        shell.ContentView = shell.LibraryViewModel;

        Assert.True(shell.IsLibraryRail);

        // And the library's game pages keep it
        shell.GameDetailViewModel.FromLibrary = true;
        shell.ContentView = shell.GameDetailViewModel;

        Assert.True(shell.IsLibraryRail);
    }

    [AvaloniaFact]
    public void RightFromTheRailFocusesTheFirstCoverInTheGrid()
    {
        using var context = FixtureContext.Create();

        var window = FixtureHost.Show(FixtureCatalog.Get("Library.Grid"), context);

        try
        {
            var rail = window.GetVisualDescendants().OfType<LibraryRail>().Single();
            rail.FocusSelectedTitle();
            Pump();

            var focused = (InputElement)window.FocusManager!.GetFocusedElement()!;
            focused.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Right });
            Pump();

            var now = (Visual)window.FocusManager!.GetFocusedElement()!;
            var cover = now as CoverButton ?? now.FindAncestorOfType<CoverButton>();

            Assert.NotNull(cover);
            Assert.Equal("Age of Empires II", ((LANCommander.Launcher.ViewModels.Components.GameItemViewModel)cover!.DataContext!).Title);
        }
        finally
        {
            window.Close();
        }
    }

    private static Button RailButton(Window window, string label) =>
        window.GetVisualDescendants()
            .OfType<NavRail>()
            .Single(r => r.IsEffectivelyVisible)
            .GetVisualDescendants()
            .OfType<Button>()
            .First(b => b.Classes.Contains("RailNav")
                && b.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == label));

    private static void Click(Window window, Control target)
    {
        var center = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;

        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);

        Pump();
    }

    // Navigation yields to the dispatcher before and after swapping the view
    private static void Pump()
    {
        for (var i = 0; i < 5; i++)
            Dispatcher.UIThread.RunJobs();
    }
}
