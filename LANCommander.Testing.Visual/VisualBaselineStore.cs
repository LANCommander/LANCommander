using System.Reflection;

namespace LANCommander.Testing.Visual;

/// <summary>
/// Where a test project's screenshots, diffs and committed baselines live, and the
/// capture-then-compare (or capture-then-accept) step shared by every visual test.
/// </summary>
/// <remarks>
/// Baselines are read from and written to the <c>Baselines/</c> folder of the test project's source
/// tree (found through the <c>ProjectDirectory</c> assembly metadata the csproj stamps in), so a
/// refreshed baseline is compared against straight away rather than after a rebuild copies it.
/// Screenshots and diffs go to the build output unless overridden, and are uploaded by CI.
/// </remarks>
public sealed class VisualBaselineStore
{
    public const string UpdateBaselinesVariable = "UPDATE_VISUAL_BASELINES";

    public static bool IsUpdatingBaselines =>
        Environment.GetEnvironmentVariable(UpdateBaselinesVariable) is "1" or "true";

    public string ScreenshotsDirectory { get; }

    public string DiffsDirectory { get; }

    public string BaselinesDirectory { get; }

    public VisualBaselineStore(string projectDirectory)
    {
        ScreenshotsDirectory = Environment.GetEnvironmentVariable("VISUAL_SCREENSHOTS_DIR")
            ?? Path.Combine(AppContext.BaseDirectory, "Screenshots");

        DiffsDirectory = Environment.GetEnvironmentVariable("VISUAL_DIFFS_DIR")
            ?? Path.Combine(AppContext.BaseDirectory, "Diffs");

        BaselinesDirectory = Environment.GetEnvironmentVariable("VISUAL_BASELINES_DIR")
            ?? Path.Combine(projectDirectory, "Baselines");
    }

    /// <summary>
    /// Creates a store for the test assembly that declares <c>ProjectDirectory</c> assembly metadata.
    /// </summary>
    public static VisualBaselineStore ForAssembly(Assembly assembly) =>
        new(assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "ProjectDirectory").Value!);

    public string GetScreenshotPath(string name)
    {
        Directory.CreateDirectory(ScreenshotsDirectory);

        return Path.Combine(ScreenshotsDirectory, $"{name}.png");
    }

    public string GetBaselinePath(string name) => Path.Combine(BaselinesDirectory, $"{name}.png");

    public string GetDiffPath(string name)
    {
        Directory.CreateDirectory(DiffsDirectory);

        return Path.Combine(DiffsDirectory, $"{name}.diff.png");
    }

    /// <summary>
    /// Compares a capture already saved at <see cref="GetScreenshotPath"/> with its baseline, or, when
    /// <see cref="UpdateBaselinesVariable"/> is set, accepts it as the new baseline.
    /// </summary>
    /// <returns>The comparison, and a failure message suited to an assertion.</returns>
    public (ComparisonResult Result, string Message) Verify(string name)
    {
        var actualPath = GetScreenshotPath(name);
        var baselinePath = GetBaselinePath(name);

        if (IsUpdatingBaselines)
        {
            Directory.CreateDirectory(BaselinesDirectory);
            File.Copy(actualPath, baselinePath, overwrite: true);

            return (ComparisonResult.Accepted(), $"Accepted '{name}' as the new baseline.");
        }

        var result = VisualComparer.Compare(actualPath, baselinePath, GetDiffPath(name));

        var message = result.BaselineExists
            ? $"'{name}': {result.Summary}"
            : $"No baseline for '{name}'. Check the capture at {actualPath}, then run the tests with " +
              $"{UpdateBaselinesVariable}=1 to accept it.";

        return (result, message);
    }
}
