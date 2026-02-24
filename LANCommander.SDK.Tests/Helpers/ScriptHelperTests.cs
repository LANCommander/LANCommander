using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;

namespace LANCommander.SDK.Tests.Helpers;

public class ScriptHelperTests : IDisposable
{
    private readonly string _tempDir;

    public ScriptHelperTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"lc-script-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private Game MakeGame(params Script[] scripts) => new()
    {
        Id = Guid.NewGuid(),
        Title = "Test Game",
        InstallDirectory = _tempDir,
        Scripts = scripts
    };

    private static Script MakeScript(ScriptType type, string contents = "Write-Host 'test'", bool requiresAdmin = false) => new()
    {
        Type = type,
        Name = type.ToString(),
        Contents = contents,
        RequiresAdmin = requiresAdmin
    };

    // ── GetScriptFileName ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(ScriptType.Install,       "Install.ps1")]
    [InlineData(ScriptType.Uninstall,     "Uninstall.ps1")]
    [InlineData(ScriptType.NameChange,    "ChangeName.ps1")]
    [InlineData(ScriptType.KeyChange,     "ChangeKey.ps1")]
    [InlineData(ScriptType.DetectInstall, "DetectInstall.ps1")]
    [InlineData(ScriptType.BeforeStart,   "BeforeStart.ps1")]
    [InlineData(ScriptType.AfterStop,     "AfterStop.ps1")]
    public void GetScriptFileName_ReturnsExpectedFilename(ScriptType type, string expected)
    {
        var result = ScriptHelper.GetScriptFileName(type);

        Assert.Equal(expected, result);
    }

    // ── GetScriptFilePath ─────────────────────────────────────────────────────

    [Fact]
    public void GetScriptFilePath_WithGuidId_ReturnsPathUnderLanCommanderMetadata()
    {
        var id = Guid.NewGuid();

        var result = ScriptHelper.GetScriptFilePath(_tempDir, id, ScriptType.Install);

        var expected = Path.Combine(_tempDir, ".lancommander", id.ToString(), "Install.ps1");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetScriptFilePath_WithStringId_ReturnsCorrectPath()
    {
        var result = ScriptHelper.GetScriptFilePath(_tempDir, "my-tool-id", ScriptType.BeforeStart);

        var expected = Path.Combine(_tempDir, ".lancommander", "my-tool-id", "BeforeStart.ps1");
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(ScriptType.Install,       "Install.ps1")]
    [InlineData(ScriptType.Uninstall,     "Uninstall.ps1")]
    [InlineData(ScriptType.AfterStop,     "AfterStop.ps1")]
    public void GetScriptFilePath_FilenameMatchesGetScriptFileName(ScriptType type, string expectedFile)
    {
        var result = ScriptHelper.GetScriptFilePath(_tempDir, Guid.NewGuid(), type);

        Assert.Equal(expectedFile, Path.GetFileName(result));
    }

    // ── GetScriptContents (Game) ──────────────────────────────────────────────

    [Fact]
    public void GetScriptContents_Game_WhenNoScripts_ReturnsEmpty()
    {
        var game = MakeGame();

        var result = ScriptHelper.GetScriptContents(game, ScriptType.Install);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GetScriptContents_Game_WhenScriptTypeNotPresent_ReturnsEmpty()
    {
        var game = MakeGame(MakeScript(ScriptType.Uninstall, "uninstall contents"));

        var result = ScriptHelper.GetScriptContents(game, ScriptType.Install);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GetScriptContents_Game_WhenScriptExists_ReturnsContents()
    {
        var game = MakeGame(MakeScript(ScriptType.Install, "Write-Host 'install'"));

        var result = ScriptHelper.GetScriptContents(game, ScriptType.Install);

        Assert.Equal("Write-Host 'install'", result);
    }

    [Fact]
    public void GetScriptContents_Game_WhenRequiresAdmin_PrependsAdminHeader()
    {
        var game = MakeGame(MakeScript(ScriptType.Install, "Write-Host 'install'", requiresAdmin: true));

        var result = ScriptHelper.GetScriptContents(game, ScriptType.Install);

        Assert.StartsWith("# Requires Admin", result);
        Assert.Contains("Write-Host 'install'", result);
    }

    [Fact]
    public void GetScriptContents_Game_WhenDoesNotRequireAdmin_DoesNotPrependHeader()
    {
        var game = MakeGame(MakeScript(ScriptType.Install, "Write-Host 'install'", requiresAdmin: false));

        var result = ScriptHelper.GetScriptContents(game, ScriptType.Install);

        Assert.DoesNotContain("# Requires Admin", result);
    }

    [Fact]
    public void GetScriptContents_Game_WhenMultipleScripts_ReturnsCorrectOne()
    {
        var game = MakeGame(
            MakeScript(ScriptType.Install,   "install contents"),
            MakeScript(ScriptType.Uninstall, "uninstall contents"));

        Assert.Equal("install contents",   ScriptHelper.GetScriptContents(game, ScriptType.Install));
        Assert.Equal("uninstall contents", ScriptHelper.GetScriptContents(game, ScriptType.Uninstall));
    }

    // ── SaveTempScriptAsync (string) ──────────────────────────────────────────

    [Fact]
    public async Task SaveTempScriptAsync_String_CreatesPs1File()
    {
        var tempPath = await ScriptHelper.SaveTempScriptAsync("Write-Host 'test'");
        try
        {
            Assert.True(File.Exists(tempPath));
            Assert.EndsWith(".ps1", tempPath);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task SaveTempScriptAsync_String_WritesExpectedContent()
    {
        var contents = "Write-Host 'hello world'";
        var tempPath = await ScriptHelper.SaveTempScriptAsync(contents);
        try
        {
            Assert.Equal(contents, await File.ReadAllTextAsync(tempPath));
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task SaveTempScriptAsync_String_EachCallCreatesDistinctFile()
    {
        var path1 = await ScriptHelper.SaveTempScriptAsync("script 1");
        var path2 = await ScriptHelper.SaveTempScriptAsync("script 2");
        try
        {
            Assert.NotEqual(path1, path2);
        }
        finally
        {
            if (File.Exists(path1)) File.Delete(path1);
            if (File.Exists(path2)) File.Delete(path2);
        }
    }

    // ── SaveTempScriptAsync (Script model) ────────────────────────────────────

    [Fact]
    public async Task SaveTempScriptAsync_Script_CreatesPs1FileWithContents()
    {
        var script = MakeScript(ScriptType.Install, "Write-Host 'from model'");
        var tempPath = await ScriptHelper.SaveTempScriptAsync(script);
        try
        {
            Assert.True(File.Exists(tempPath));
            Assert.EndsWith(".ps1", tempPath);
            Assert.Equal(script.Contents, await File.ReadAllTextAsync(tempPath));
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    // ── SaveScriptAsync (Game) ────────────────────────────────────────────────

    [Fact]
    public async Task SaveScriptAsync_Game_WhenScriptExists_WritesFileAtExpectedPath()
    {
        var script = MakeScript(ScriptType.Install, "Write-Host 'install'");
        var game = MakeGame(script);

        await ScriptHelper.SaveScriptAsync(game, ScriptType.Install, _tempDir);

        var expectedPath = ScriptHelper.GetScriptFilePath(_tempDir, game.Id, ScriptType.Install);
        Assert.True(File.Exists(expectedPath));
        Assert.Equal("Write-Host 'install'", await File.ReadAllTextAsync(expectedPath));
    }

    [Fact]
    public async Task SaveScriptAsync_Game_WhenScriptDoesNotExist_DoesNotCreateFile()
    {
        var game = MakeGame(); // no scripts

        await ScriptHelper.SaveScriptAsync(game, ScriptType.Install, _tempDir);

        var path = ScriptHelper.GetScriptFilePath(_tempDir, game.Id, ScriptType.Install);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task SaveScriptAsync_Game_CreatesParentDirectoriesAsNeeded()
    {
        var script = MakeScript(ScriptType.Uninstall, "Write-Host 'uninstall'");
        var game = MakeGame(script);
        var nestedInstallDir = Path.Combine(_tempDir, "nested", "install");

        await ScriptHelper.SaveScriptAsync(game, ScriptType.Uninstall, nestedInstallDir);

        var expectedPath = ScriptHelper.GetScriptFilePath(nestedInstallDir, game.Id, ScriptType.Uninstall);
        Assert.True(File.Exists(expectedPath));
    }

    [Fact]
    public async Task SaveScriptAsync_Game_OverwritesExistingFile()
    {
        var game = MakeGame(MakeScript(ScriptType.Install, "first"));
        await ScriptHelper.SaveScriptAsync(game, ScriptType.Install, _tempDir);

        // Replace the script on the game object and save again
        game.Scripts = new[] { MakeScript(ScriptType.Install, "second") };
        await ScriptHelper.SaveScriptAsync(game, ScriptType.Install, _tempDir);

        var path = ScriptHelper.GetScriptFilePath(_tempDir, game.Id, ScriptType.Install);
        Assert.Equal("second", await File.ReadAllTextAsync(path));
    }
}
