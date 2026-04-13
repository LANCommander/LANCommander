using LANCommander.SDK.Services;

namespace LANCommander.SDK.Tests.Install;

public class InstallResultTests
{
    [Fact]
    public void Constructor_WithDirectoryAndId_SetsInstallDirectory()
    {
        var gameId = Guid.NewGuid();
        var dir = @"C:\Games\MyGame";

        var result = new InstallResult(dir, gameId);

        Assert.Equal(dir, result.InstallDirectory);
    }

    [Fact]
    public void Constructor_WithDirectoryAndId_CreatesFileListWithCorrectDirectory()
    {
        var gameId = Guid.NewGuid();
        var dir = @"C:\Games\MyGame";

        var result = new InstallResult(dir, gameId);

        Assert.NotNull(result.FileList);
        Assert.Equal(dir, result.FileList.InstallDirectory);
    }

    [Fact]
    public void Constructor_WithDirectoryAndId_CreatesFileListWithCorrectGameId()
    {
        var gameId = Guid.NewGuid();
        var dir = @"C:\Games\MyGame";

        var result = new InstallResult(dir, gameId);

        Assert.Equal(gameId, result.FileList.BaseGame.GameId);
    }

    [Fact]
    public void DefaultConstructor_InitializesWithEmptyFileList()
    {
        var result = new InstallResult();

        Assert.NotNull(result.FileList);
    }

}
