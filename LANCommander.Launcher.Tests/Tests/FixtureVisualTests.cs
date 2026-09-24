using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using LANCommander.Launcher.Fixtures;
using LANCommander.Launcher.Tests.Helpers;
using LANCommander.Launcher.Views;
using Xunit;

namespace LANCommander.Launcher.Tests.Tests;

/// <summary>
/// Renders every fixture in <see cref="FixtureCatalog"/> and compares it with its baseline in
/// Baselines/, named after the fixture. Fixtures build the real main window, shell, pages and
/// overlays from canned data, so these cover the launcher as a user sees it.
/// </summary>
/// <remarks>
/// To create or refresh baselines, run with <c>UPDATE_VISUAL_BASELINES=1</c> (see
/// <see cref="VisualAssert"/>). To look at a fixture in the running app, start a Debug build with
/// <c>LANCOMMANDER_FIXTURE</c> set to its name.
/// </remarks>
public class FixtureVisualTests
{
    /// <summary>
    /// Views no fixture needs to show. Anything else in LANCommander.Launcher.Views that no fixture
    /// renders fails <see cref="EveryViewAppearsInAFixture"/>.
    /// </summary>
    private static readonly Dictionary<Type, string> UncoveredViews = new()
    {
        [typeof(VideoPlayerOverlay)] = "Unused, and plays through LibVLC as soon as it has a video, so it can't render the same way twice.",
    };

    public static TheoryData<string> Fixtures => new(FixtureCatalog.All.Select(f => f.Name));

    [AvaloniaTheory]
    [MemberData(nameof(Fixtures))]
    public void FixtureMatchesBaseline(string name)
    {
        using var context = FixtureContext.Create();

        var window = FixtureHost.Show(FixtureCatalog.Get(name), context);

        try
        {
            VisualAssert.MatchesBaseline(window, name);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void FixtureNamesAreUnique()
    {
        var duplicates = FixtureCatalog.All
            .GroupBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.True(duplicates.Count == 0, $"Duplicate fixture names: {string.Join(", ", duplicates)}");
    }

    [AvaloniaFact]
    public void EveryViewAppearsInAFixture()
    {
        var rendered = new HashSet<Type>();

        foreach (var fixture in FixtureCatalog.All)
        {
            using var context = FixtureContext.Create();

            var window = FixtureHost.Show(fixture, context);

            rendered.Add(window.GetType());

            foreach (var visual in window.GetVisualDescendants())
                rendered.Add(visual.GetType());

            window.Close();
        }

        var missing = typeof(MainWindow).Assembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith("LANCommander.Launcher.Views", StringComparison.Ordinal) == true)
            .Where(t => !t.IsAbstract && (typeof(UserControl).IsAssignableFrom(t) || typeof(Window).IsAssignableFrom(t)))
            .Where(t => !rendered.Contains(t) && !UncoveredViews.ContainsKey(t))
            .Select(t => t.FullName)
            .OrderBy(n => n)
            .ToList();

        Assert.True(missing.Count == 0,
            "No fixture renders these views. Add a fixture to LANCommander.Launcher/Fixtures, or list the " +
            $"view in {nameof(UncoveredViews)} with the reason:{Environment.NewLine}{string.Join(Environment.NewLine, missing)}");
    }
}
