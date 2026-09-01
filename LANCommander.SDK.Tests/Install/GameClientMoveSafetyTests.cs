using LANCommander.SDK.Enums;
using LANCommander.SDK.Services;
using SdkGame = LANCommander.SDK.Models.Game;

namespace LANCommander.SDK.Tests.Install;

/// <summary>
/// Regression coverage for the CRITICAL destructive-update bug: a caller that mis-resolves a
/// "move" destination to be the same as, or nested under, the source directory would previously
/// have its files copied into the nested destination and then have the source (including the
/// just-made copies) recursively deleted by <see cref="GameClient.MoveAsync(Models.Game, string, string)"/>
/// — total data loss. <see cref="GameClient.IsSameOrNestedPath"/> is the guard that now rejects
/// this outright, both as a pure predicate and wired into MoveAsync itself.
/// </summary>
public class GameClientMoveSafetyTests : IDisposable
{
    private readonly string _tempDir;

    public GameClientMoveSafetyTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"lc-move-safety-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private static GameClient CreateClient() =>
        new(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);

    // ── IsSameOrNestedPath (pure predicate) ────────────────────────────────────

    [Theory]
    [InlineData(@"C:\Games\HalfLife", @"C:\Games\HalfLife", true)]
    [InlineData(@"C:\Games\HalfLife", @"C:\Games\HalfLife\", true)]
    [InlineData(@"C:\Games\HalfLife", @"C:\Games\HALFLIFE", true)]
    [InlineData(@"C:\Games\HalfLife", @"C:\Games\HalfLife\HalfLife", true)]
    [InlineData(@"C:\Games\HalfLife", @"C:\Games\HalfLife\Nested\Deeper", true)]
    [InlineData(@"C:\Games\HalfLife", @"C:\Games\HalfLife2", false)]
    [InlineData(@"C:\Games\HalfLife", @"C:\Games\Other", false)]
    [InlineData(@"C:\Games\HalfLife", @"C:\Games", false)]
    public void IsSameOrNestedPath_detects_equal_and_nested_destinations(string basePath, string candidatePath, bool expected)
    {
        Assert.Equal(expected, GameClient.IsSameOrNestedPath(basePath, candidatePath));
    }

    // ── MoveAsync rejection ─────────────────────────────────────────────────────

    [Fact]
    public async Task MoveAsync_RejectsDestinationNestedUnderSource_AndLeavesSourceIntact()
    {
        var client = CreateClient();

        var oldDirectory = Path.Combine(_tempDir, "HalfLife");
        Directory.CreateDirectory(oldDirectory);
        var markerPath = Path.Combine(oldDirectory, "marker.txt");
        await File.WriteAllTextAsync(markerPath, "irreplaceable save data");

        // Reproduces the exact mechanism of the destructive update bug: a destination computed
        // as "the existing install directory, re-suffixed with the game's own title" — nested
        // one level under the source instead of being a sibling/parent-relative path.
        var nestedDestination = Path.Combine(oldDirectory, "HalfLife");

        var game = new SdkGame { Id = Guid.NewGuid(), Title = "Half-Life", Type = GameType.MainGame, BaseGameId = Guid.Empty, DependentGames = [] };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.MoveAsync(game, oldDirectory, nestedDestination));

        // The source directory and its contents must survive completely untouched.
        Assert.True(Directory.Exists(oldDirectory));
        Assert.True(File.Exists(markerPath));
        Assert.Equal("irreplaceable save data", await File.ReadAllTextAsync(markerPath));
        Assert.False(Directory.Exists(nestedDestination));
    }

    [Fact]
    public async Task MoveAsync_RejectsDestinationEqualToSource_AndLeavesSourceIntact()
    {
        var client = CreateClient();

        var installDirectory = Path.Combine(_tempDir, "HalfLife");
        Directory.CreateDirectory(installDirectory);
        var markerPath = Path.Combine(installDirectory, "marker.txt");
        await File.WriteAllTextAsync(markerPath, "irreplaceable save data");

        var game = new SdkGame { Id = Guid.NewGuid(), Title = "Half-Life", Type = GameType.MainGame, BaseGameId = Guid.Empty, DependentGames = [] };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.MoveAsync(game, installDirectory, installDirectory));

        Assert.True(Directory.Exists(installDirectory));
        Assert.True(File.Exists(markerPath));
    }

    [Fact]
    public async Task MoveAsync_StillSucceeds_ForALegitimateSiblingDestination()
    {
        // Contrast case: a genuine sibling/unrelated destination must still work exactly as
        // before — the new guard must not be overly broad. MoveAsync copies subdirectories
        // before flat files and never creates the destination root itself, so (matching
        // InstallServiceQueueTests' Move fixture) the marker lives in a subdirectory rather than
        // directly under the root.
        var client = CreateClient();

        var oldDirectory = Path.Combine(_tempDir, "Old", "HalfLife");
        Directory.CreateDirectory(Path.Combine(oldDirectory, "data"));
        await File.WriteAllTextAsync(Path.Combine(oldDirectory, "data", "marker.txt"), "hello");

        var newDirectory = Path.Combine(_tempDir, "New", "HalfLife");

        var game = new SdkGame { Id = Guid.NewGuid(), Title = "Half-Life", Type = GameType.MainGame, BaseGameId = Guid.Empty, DependentGames = [] };

        var result = await client.MoveAsync(game, oldDirectory, newDirectory);

        Assert.Equal(newDirectory, result);
        Assert.False(Directory.Exists(oldDirectory));
        Assert.True(File.Exists(Path.Combine(newDirectory, "data", "marker.txt")));
    }

    // ── IsOverlayInstallType ────────────────────────────────────────────────────

    [Theory]
    [InlineData(GameType.MainGame, false)]
    [InlineData(GameType.StandaloneExpansion, false)]
    [InlineData(GameType.Expansion, true)]
    [InlineData(GameType.Mod, true)]
    [InlineData(GameType.StandaloneMod, true)]
    public void IsOverlayInstallType_MatchesGetInstallDirectorySharingRule_WhenBaseGameIdIsSet(GameType type, bool expectedOverlay)
    {
        var game = new SdkGame { Id = Guid.NewGuid(), Title = "Some Addon", Type = type, BaseGameId = Guid.NewGuid() };

        Assert.Equal(expectedOverlay, GameClient.IsOverlayInstallType(game));
    }

    [Theory]
    [InlineData(GameType.Expansion)]
    [InlineData(GameType.Mod)]
    [InlineData(GameType.StandaloneMod)]
    public void IsOverlayInstallType_FalseWithoutABaseGameId_EvenForOtherwiseOverlayTypes(GameType type)
    {
        var game = new SdkGame { Id = Guid.NewGuid(), Title = "Orphaned", Type = type, BaseGameId = Guid.Empty };

        Assert.False(GameClient.IsOverlayInstallType(game));
    }
}
