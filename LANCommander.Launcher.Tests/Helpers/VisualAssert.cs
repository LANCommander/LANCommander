using System;
using System.IO;
using Avalonia.Controls;
using Xunit;

namespace LANCommander.Launcher.Tests.Helpers;

/// <summary>
/// Captures a window and compares it with its committed baseline.
/// </summary>
/// <remarks>
/// Run with <c>UPDATE_VISUAL_BASELINES=1</c> to write every capture over its baseline in the source
/// tree instead of comparing, which is how new baselines are created and accepted ones refreshed.
/// </remarks>
public static class VisualAssert
{
    public const string UpdateBaselinesVariable = "UPDATE_VISUAL_BASELINES";

    public static bool IsUpdatingBaselines =>
        Environment.GetEnvironmentVariable(UpdateBaselinesVariable) is "1" or "true";

    public static void MatchesBaseline(TopLevel window, string name)
    {
        var actualPath = ScreenshotHelper.Capture(window, name);
        var baselinePath = ScreenshotHelper.GetBaselinePath(name);

        if (IsUpdatingBaselines)
        {
            Directory.CreateDirectory(ScreenshotHelper.BaselinesDirectory);
            File.Copy(actualPath, baselinePath, overwrite: true);

            return;
        }

        var result = VisualComparer.Compare(actualPath, baselinePath, ScreenshotHelper.GetDiffPath(name));

        var message = result.BaselineExists
            ? result.Summary
            : $"No baseline for '{name}'. Check the capture at {actualPath}, then run the tests with " +
              $"{UpdateBaselinesVariable}=1 to accept it.";

        Assert.True(result.Passed, message);
    }
}
