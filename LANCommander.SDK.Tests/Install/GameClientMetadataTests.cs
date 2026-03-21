using LANCommander.SDK.Services;

namespace LANCommander.SDK.Tests.Install;

/// <summary>
/// Tests for GameClient static helper methods that operate on local metadata files.
/// These methods are file-system only and require no API connection.
/// </summary>
public class GameClientMetadataTests : IDisposable
{
    private readonly string _tempDir;

    public GameClientMetadataTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"lc-sdk-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    // ── GetMetadataDirectoryPath ──────────────────────────────────────────────

    [Fact]
    public void GetMetadataDirectoryPath_WithValidInputs_ReturnsCorrectPath()
    {
        var gameId = Guid.NewGuid();
        var expected = Path.Combine(@"C:\Games\TestGame", ".lancommander", gameId.ToString());

        var result = GameClient.GetMetadataDirectoryPath(@"C:\Games\TestGame", gameId);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void GetMetadataDirectoryPath_WithEmptyOrNullInstallDirectory_ReturnsEmptyString(string installDirectory)
    {
        var result = GameClient.GetMetadataDirectoryPath(installDirectory, Guid.NewGuid());

        Assert.Equal("", result);
    }

    // ── GetMetadataFilePath ───────────────────────────────────────────────────

    [Fact]
    public void GetMetadataFilePath_ReturnsPathInsideMetadataDirectory()
    {
        var gameId = Guid.NewGuid();
        var expected = Path.Combine(@"C:\Games\TestGame", ".lancommander", gameId.ToString(), "Manifest.yml");

        var result = GameClient.GetMetadataFilePath(@"C:\Games\TestGame", gameId, "Manifest.yml");

        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetMetadataFilePath_WithDifferentFileNames_ReturnsCorrectPath()
    {
        var gameId = Guid.NewGuid();
        var installDir = @"C:\Games\MyGame";

        var manifestPath = GameClient.GetMetadataFilePath(installDir, gameId, "Manifest.yml");
        var fileListPath = GameClient.GetMetadataFilePath(installDir, gameId, "FileList.txt");

        Assert.EndsWith("Manifest.yml", manifestPath);
        Assert.EndsWith("FileList.txt", fileListPath);
        Assert.NotEqual(manifestPath, fileListPath);
    }

    // ── GetPlayerAlias / UpdatePlayerAlias ────────────────────────────────────

    [Fact]
    public void GetPlayerAlias_WhenFileDoesNotExist_ReturnsEmptyString()
    {
        var gameId = Guid.NewGuid();

        var alias = GameClient.GetPlayerAlias(_tempDir, gameId);

        Assert.Equal(string.Empty, alias);
    }

    [Fact]
    public void UpdatePlayerAlias_WritesAliasToFile()
    {
        var gameId = Guid.NewGuid();
        EnsureMetadataDirectoryExists(gameId);

        GameClient.UpdatePlayerAlias(_tempDir, gameId, "TestPlayer");

        var alias = GameClient.GetPlayerAlias(_tempDir, gameId);
        Assert.Equal("TestPlayer", alias);
    }

    [Fact]
    public void UpdatePlayerAlias_OverwritesPreviousAlias()
    {
        var gameId = Guid.NewGuid();
        EnsureMetadataDirectoryExists(gameId);

        GameClient.UpdatePlayerAlias(_tempDir, gameId, "OldName");
        GameClient.UpdatePlayerAlias(_tempDir, gameId, "NewName");

        Assert.Equal("NewName", GameClient.GetPlayerAlias(_tempDir, gameId));
    }

    [Fact]
    public async Task GetPlayerAliasAsync_WhenFileDoesNotExist_ReturnsEmptyString()
    {
        var gameId = Guid.NewGuid();

        var alias = await GameClient.GetPlayerAliasAsync(_tempDir, gameId);

        Assert.Equal(string.Empty, alias);
    }

    [Fact]
    public async Task UpdatePlayerAliasAsync_WritesAliasToFile()
    {
        var gameId = Guid.NewGuid();
        EnsureMetadataDirectoryExists(gameId);

        await GameClient.UpdatePlayerAliasAsync(_tempDir, gameId, "AsyncPlayer");

        var alias = await GameClient.GetPlayerAliasAsync(_tempDir, gameId);
        Assert.Equal("AsyncPlayer", alias);
    }

    // ── GetCurrentKey / UpdateCurrentKey ──────────────────────────────────────

    [Fact]
    public void GetCurrentKey_WhenFileDoesNotExist_ReturnsEmptyString()
    {
        var gameId = Guid.NewGuid();

        var key = GameClient.GetCurrentKey(_tempDir, gameId);

        Assert.Equal(string.Empty, key);
    }

    [Fact]
    public void UpdateCurrentKey_WritesKeyToFile()
    {
        var gameId = Guid.NewGuid();
        EnsureMetadataDirectoryExists(gameId);

        GameClient.UpdateCurrentKey(_tempDir, gameId, "XXXX-YYYY-ZZZZ");

        var key = GameClient.GetCurrentKey(_tempDir, gameId);
        Assert.Equal("XXXX-YYYY-ZZZZ", key);
    }

    [Fact]
    public void UpdateCurrentKey_OverwritesPreviousKey()
    {
        var gameId = Guid.NewGuid();
        EnsureMetadataDirectoryExists(gameId);

        GameClient.UpdateCurrentKey(_tempDir, gameId, "OLD-KEY-1234");
        GameClient.UpdateCurrentKey(_tempDir, gameId, "NEW-KEY-5678");

        Assert.Equal("NEW-KEY-5678", GameClient.GetCurrentKey(_tempDir, gameId));
    }

    [Fact]
    public async Task GetCurrentKeyAsync_WhenFileDoesNotExist_ReturnsEmptyString()
    {
        var gameId = Guid.NewGuid();

        var key = await GameClient.GetCurrentKeyAsync(_tempDir, gameId);

        Assert.Equal(string.Empty, key);
    }

    [Fact]
    public async Task UpdateCurrentKeyAsync_WritesKeyToFile()
    {
        var gameId = Guid.NewGuid();
        EnsureMetadataDirectoryExists(gameId);

        await GameClient.UpdateCurrentKeyAsync(_tempDir, gameId, "ASYNC-KEY-ABCD");

        var key = await GameClient.GetCurrentKeyAsync(_tempDir, gameId);
        Assert.Equal("ASYNC-KEY-ABCD", key);
    }

    private void EnsureMetadataDirectoryExists(Guid gameId)
    {
        var metaDir = GameClient.GetMetadataDirectoryPath(_tempDir, gameId);
        Directory.CreateDirectory(metaDir);
    }
}
