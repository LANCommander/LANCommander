using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Headless;

namespace LANCommander.Launcher.Avalonia.Tests.Helpers;

/// <summary>
/// Captures and persists screenshots from headless Avalonia windows.
/// </summary>
public static class ScreenshotHelper
{
    /// <summary>
    /// Directory where actual screenshots from the current test run are written.
    /// Uploaded as a CI artifact; not committed to source control.
    /// </summary>
    public static string ScreenshotsDirectory { get; } =
        Environment.GetEnvironmentVariable("VISUAL_SCREENSHOTS_DIR")
        ?? Path.Combine(AppContext.BaseDirectory, "Screenshots");

    /// <summary>
    /// Directory where diff images are written when a regression is detected.
    /// Uploaded as a CI artifact; not committed to source control.
    /// </summary>
    public static string DiffsDirectory { get; } =
        Environment.GetEnvironmentVariable("VISUAL_DIFFS_DIR")
        ?? Path.Combine(AppContext.BaseDirectory, "Diffs");

    /// <summary>
    /// Directory containing the committed baseline PNG files.
    /// Populated from the Baselines/ folder in the test project (Content items).
    /// </summary>
    public static string BaselinesDirectory { get; } =
        Environment.GetEnvironmentVariable("VISUAL_BASELINES_DIR")
        ?? Path.Combine(AppContext.BaseDirectory, "Baselines");

    /// <summary>
    /// Renders the window, saves the screenshot under <paramref name="name"/>.png,
    /// and returns the full path so the caller can pass it to <see cref="VisualComparer"/>.
    /// </summary>
    public static string Capture(TopLevel window, string name)
    {
        Directory.CreateDirectory(ScreenshotsDirectory);

        var bitmap = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException($"CaptureRenderedFrame returned null for '{name}'.");

        var path = Path.Combine(ScreenshotsDirectory, $"{name}.png");
        bitmap.Save(path);
        return path;
    }

    /// <summary>
    /// Returns the path to the baseline file for <paramref name="name"/>.
    /// The file may not exist yet if this is the first run.
    /// </summary>
    public static string GetBaselinePath(string name) =>
        Path.Combine(BaselinesDirectory, $"{name}.png");

    /// <summary>
    /// Returns the path where a diff image should be written for <paramref name="name"/>.
    /// </summary>
    public static string GetDiffPath(string name)
    {
        Directory.CreateDirectory(DiffsDirectory);
        return Path.Combine(DiffsDirectory, $"{name}.diff.png");
    }
}
