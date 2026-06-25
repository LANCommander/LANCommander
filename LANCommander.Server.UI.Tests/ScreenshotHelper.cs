using System.Reflection;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace LANCommander.Server.UI.Tests;

/// <summary>
/// Captures a full-page screenshot of the final page state at the end of each test.
/// Screenshots are saved to a "Screenshots" directory that CI uploads as an artifact,
/// making failures easy to diagnose. (xUnit v2 does not expose the test outcome to
/// DisposeAsync, so we capture unconditionally and name each file after the test.)
/// </summary>
public static class ScreenshotHelper
{
    private static readonly string ScreenshotDir = Path.Combine(
        Environment.GetEnvironmentVariable("SCREENSHOT_DIR")
            ?? Path.Combine(AppContext.BaseDirectory, "Screenshots"),
        string.Empty);

    /// <summary>
    /// Captures a screenshot of the current page state, named after the running test.
    /// Call this from DisposeAsync — it extracts the test name from ITestOutputHelper.
    /// </summary>
    public static async Task CaptureAsync(IPage? page, ITestOutputHelper? output)
    {
        if (page == null || output == null)
            return;

        var testName = GetTestDisplayName(output) ?? $"Unknown_{Guid.NewGuid():N}";

        try
        {
            Directory.CreateDirectory(ScreenshotDir);
            var safeName = string.Join("_", testName.Split(Path.GetInvalidFileNameChars()));
            var path = Path.Combine(ScreenshotDir, $"{safeName}.png");
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = path,
                FullPage = true
            });
        }
        catch
        {
            // Best effort — don't fail the test because of screenshot capture
        }
    }

    /// <summary>
    /// Extracts the test display name from xUnit's ITestOutputHelper via reflection.
    /// </summary>
    private static string? GetTestDisplayName(ITestOutputHelper output)
    {
        try
        {
            var type = output.GetType();
            var testField = type.GetField("test", BindingFlags.Instance | BindingFlags.NonPublic);
            if (testField == null)
                return null;

            var test = testField.GetValue(output);
            var displayNameProp = test?.GetType().GetProperty("DisplayName");
            return displayNameProp?.GetValue(test) as string;
        }
        catch
        {
            return null;
        }
    }
}
