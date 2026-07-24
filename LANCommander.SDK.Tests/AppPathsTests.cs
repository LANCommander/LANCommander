using LANCommander.SDK.Helpers;

namespace LANCommander.SDK.Tests;

public class AppPathsTests
{
    // ── ResolveStorageLocationPath: rooted paths ─────────────────────────────

    [Fact]
    public void ResolveStorageLocationPath_RootedPath_ReturnedAsIs()
    {
        var rooted = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);

        var resolved = AppPaths.ResolveStorageLocationPath(rooted);

        Assert.Equal(rooted, resolved);
    }

    [Fact]
    public void ResolveStorageLocationPath_RootedPathWithSegments_CombinesUnderRoot()
    {
        var rooted = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);

        var resolved = AppPaths.ResolveStorageLocationPath(rooted, "user", "game", "save");

        Assert.Equal(Path.Combine(rooted, "user", "game", "save"), resolved);
    }

    // ── ResolveStorageLocationPath: relative paths anchor to the config dir ───

    [Fact]
    public void ResolveStorageLocationPath_RelativePath_AnchoredToConfigDirectory()
    {
        var resolved = AppPaths.ResolveStorageLocationPath("Saves");

        Assert.Equal(Path.Combine(AppPaths.GetConfigDirectory(), "Saves"), resolved);
    }

    [Fact]
    public void ResolveStorageLocationPath_RelativePathWithSegments_AnchoredToConfigDirectory()
    {
        var resolved = AppPaths.ResolveStorageLocationPath("Saves", "user", "game", "save");

        Assert.Equal(
            Path.Combine(AppPaths.GetConfigDirectory(), "Saves", "user", "game", "save"),
            resolved);
    }

    /// <summary>
    /// Regression guard for the reported bug: writes and reads of the same save both went through two
    /// different resolvers that disagreed for relative storage paths (one anchored to the working
    /// directory, the other to the config directory). Every consumer must now resolve identically.
    /// </summary>
    [Fact]
    public void ResolveStorageLocationPath_SameRelativeInput_IsDeterministicAcrossCallers()
    {
        var writer = AppPaths.ResolveStorageLocationPath("Saves", "user", "game", "save");
        var reader = AppPaths.ResolveStorageLocationPath("Saves", "user", "game", "save");

        Assert.Equal(writer, reader);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveStorageLocationPath_NullOrWhitespacePath_Throws(string? path)
    {
        Assert.Throws<ArgumentException>(() => AppPaths.ResolveStorageLocationPath(path!));
    }

    // ── GetConfigDirectory ───────────────────────────────────────────────────

    [Fact]
    public void GetConfigDirectory_ReturnsAbsoluteExistingDirectory()
    {
        var configDir = AppPaths.GetConfigDirectory();

        Assert.True(Path.IsPathRooted(configDir));
        Assert.True(Directory.Exists(configDir));
    }

    /// <summary>
    /// With no override, the data root is a "Data" folder under the current working directory when writable.
    /// </summary>
    [Fact]
    public void GetConfigDirectory_AnchoredToWorkingDirectory()
    {
        // Skip when an operator override or a read-only working directory changes the anchor.
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(AppPaths.DataDirectoryEnvironmentVariable)))
            return;

        var workingDir = Directory.GetCurrentDirectory();

        if (!DirectoryHelper.IsDirectoryWritable(workingDir))
            return;

        var configDir = Path.GetFullPath(AppPaths.GetConfigDirectory());

        Assert.Equal(Path.GetFullPath(Path.Combine(workingDir, "Data")), configDir);
    }
}
