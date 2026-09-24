#if DEBUG
using System;
using Avalonia.Controls;

namespace LANCommander.Launcher.Fixtures;

/// <summary>
/// One screen of the launcher in one state, built from canned data so it renders the same way every
/// time. Visual regression tests capture every fixture in <see cref="FixtureCatalog"/> and compare it
/// against a committed baseline; set <c>LANCOMMANDER_FIXTURE</c> on a Debug build to open one in the
/// running app instead.
/// </summary>
/// <param name="Name">
/// Unique, dotted name such as <c>Library.Grid</c>. Also the baseline's file name, so renaming a
/// fixture orphans its baseline.
/// </param>
/// <param name="Description">What state the fixture shows, for the preview gallery.</param>
/// <param name="Create">
/// Builds the content. Return a <see cref="Window"/> to use it as is (the main window, dialogs);
/// any other control is hosted in a plain window of <see cref="Width"/> by <see cref="Height"/>.
/// </param>
public sealed record ViewFixture(string Name, string Description, Func<FixtureContext, Control> Create)
{
    public double Width { get; init; } = 1200;

    public double Height { get; init; } = 800;

    /// <summary>
    /// Runs once the window is shown and laid out, for state that only exists in the visual tree:
    /// opening an overlay, selecting a tab, scrolling.
    /// </summary>
    public Action<FixtureContext, Window>? Prepare { get; init; }

    public override string ToString() => Name;
}
#endif
