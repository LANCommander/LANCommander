#if DEBUG
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LANCommander.Launcher.Helpers;
using LiveChartsCore.Kernel;
using LiveChartsCore.Kernel.Sketches;

namespace LANCommander.Launcher.Fixtures;

/// <summary>Puts a fixture on screen the same way for the visual tests and the in-app preview.</summary>
public static class FixtureHost
{
    /// <summary>Long enough for a page of covers to decode on a slow CI machine.</summary>
    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(15);

    public static Window Show(ViewFixture fixture, FixtureContext context)
    {
        var content = fixture.Create(context);

        var window = content as Window ?? new Window { Content = content };

        window.Width = fixture.Width;
        window.Height = fixture.Height;
        window.Title = fixture.Name;

        window.Show();

        Settle(window);
        DrawCharts(window);
        Settle(window);
        StopTransitions(window);

        fixture.Prepare?.Invoke(context, window);

        // Views that focus a text box on load (login, server selection) would otherwise blink a
        // caret into some captures and not others.
        if (window.FocusManager?.GetFocusedElement() is TextBox)
            window.FocusManager.ClearFocus();

        Settle(window);
        StopTransitions(window);
        Settle(window);

        return window;
    }

    /// <summary>
    /// Charts measure on a throttled timer of their own, and the first chart in a process takes
    /// longer than the rest, so a capture can find one still blank. Update them now instead.
    /// </summary>
    private static void DrawCharts(Window window)
    {
        foreach (var chart in window.GetVisualDescendants().OfType<IChartView>())
            chart.CoreChart.Update(new ChartUpdateParams { IsAutomaticUpdate = false, Throttling = false });
    }

    /// <summary>
    /// Removes every transition in the window. Transitions run on the clock, so one still running
    /// (a chevron turning as an expander settles, a button fading in) would be caught at a different
    /// point in each capture; removing it jumps the property to where it was heading.
    /// </summary>
    private static void StopTransitions(Window window)
    {
        foreach (var visual in window.GetVisualDescendants().Prepend(window))
        {
            if (visual is Animatable { Transitions.Count: > 0 } animatable)
                animatable.Transitions = null;
        }
    }

    /// <summary>
    /// Runs the dispatcher until layout is done and every image that started loading has landed.
    /// Covers and backgrounds decode off the UI thread and arrive at background priority, often
    /// after layout has asked for them, so one pass is never enough.
    /// </summary>
    public static void Settle(Window window)
    {
        var timeout = Stopwatch.StartNew();
        var quietPasses = 0;

        // A few quiet passes in a row, because a finished load can lay out new content that starts
        // another (a carousel realizing its next page, say).
        while (quietPasses < 3)
        {
            if (timeout.Elapsed > SettleTimeout)
                throw new TimeoutException($"'{window.Title}' still had images loading after {SettleTimeout.TotalSeconds:0} seconds.");

            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            if (PendingLoads.Any)
            {
                quietPasses = 0;
                Thread.Sleep(10);
            }
            else
            {
                quietPasses++;
            }
        }
    }
}
#endif
