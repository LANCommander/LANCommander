using LANCommander.SDK.Services;
using ManifestGame = LANCommander.SDK.Models.Manifest.Game;

namespace LANCommander.SDK.Tests.Install;

public class GameInstallationFileListTests
{
    private static GameInstallationFileList MakeFileList(string installDir, Guid gameId, params string[] filePaths)
    {
        var list = new GameInstallationFileList(installDir, gameId);
        list.BaseGame.AddFiles(filePaths.Select(p => new GameInstallationFileListEntry.FileEntry
        {
            EntryPath = p,
            LocalPath = p
        }));
        return list;
    }

    // ── MergeBase ────────────────────────────────────────────────────────────

    [Fact]
    public void MergeBase_MergesBaseGameFilesIntoTarget()
    {
        var gameId = Guid.NewGuid();
        var target = MakeFileList(@"C:\Games\TestGame", gameId, "game.exe");
        var source = MakeFileList(@"C:\Games\TestGame", gameId, "data.pak");

        target.MergeBase(source);

        Assert.Equal(2, target.BaseGame.Files.Count);
    }

    [Fact]
    public void MergeBase_DoesNotDuplicateExistingFiles()
    {
        var gameId = Guid.NewGuid();
        var target = MakeFileList(@"C:\Games\TestGame", gameId, "game.exe");
        var source = MakeFileList(@"C:\Games\TestGame", gameId, "game.exe", "readme.txt");

        target.MergeBase(source);

        Assert.Equal(2, target.BaseGame.Files.Count);
    }

    // ── MergeBaseAsDependentGame ──────────────────────────────────────────────

    [Fact]
    public void MergeBaseAsDependentGame_CreatesDependentGameEntry()
    {
        var baseGameId = Guid.NewGuid();
        var addonId = Guid.NewGuid();
        var target = MakeFileList(@"C:\Games\TestGame", baseGameId);
        var addonFileList = MakeFileList(@"C:\Games\TestGame", addonId, "addon.pak");

        target.MergeBaseAsDependentGame(addonId, addonFileList);

        Assert.True(target.DependentGames.ContainsKey(addonId));
    }

    [Fact]
    public void MergeBaseAsDependentGame_CopiesFilesFromSourceBaseGame()
    {
        var baseGameId = Guid.NewGuid();
        var addonId = Guid.NewGuid();
        var target = MakeFileList(@"C:\Games\TestGame", baseGameId);
        var addonFileList = MakeFileList(@"C:\Games\TestGame", addonId, "addon.pak", "addon_data.pak");

        target.MergeBaseAsDependentGame(addonId, addonFileList);

        Assert.Equal(2, target.DependentGames[addonId].BaseGame.Files.Count);
    }

    [Fact]
    public void MergeBaseAsDependentGame_SetsManifestOnNewDependentGame()
    {
        var baseGameId = Guid.NewGuid();
        var addonId = Guid.NewGuid();
        var manifest = new ManifestGame { Id = addonId, Title = "Test Addon" };
        var target = MakeFileList(@"C:\Games\TestGame", baseGameId);
        var addonFileList = MakeFileList(@"C:\Games\TestGame", addonId);
        addonFileList.BaseGame.Manifest = manifest;

        target.MergeBaseAsDependentGame(addonId, addonFileList);

        Assert.Equal(manifest, target.DependentGames[addonId].BaseGame.Manifest);
    }

    [Fact]
    public void MergeBaseAsDependentGame_MergesIntoExistingDependentGame()
    {
        var baseGameId = Guid.NewGuid();
        var addonId = Guid.NewGuid();
        var target = MakeFileList(@"C:\Games\TestGame", baseGameId);

        var firstAddonFiles = MakeFileList(@"C:\Games\TestGame", addonId, "file1.pak");
        target.MergeBaseAsDependentGame(addonId, firstAddonFiles);

        var secondAddonFiles = MakeFileList(@"C:\Games\TestGame", addonId, "file2.pak");
        target.MergeBaseAsDependentGame(addonId, secondAddonFiles);

        Assert.Single(target.DependentGames);
        Assert.Equal(2, target.DependentGames[addonId].BaseGame.Files.Count);
    }

    [Fact]
    public void MergeBaseAsDependentGame_DoesNotDuplicateFilesOnSecondMerge()
    {
        var baseGameId = Guid.NewGuid();
        var addonId = Guid.NewGuid();
        var target = MakeFileList(@"C:\Games\TestGame", baseGameId);

        var addonFiles = MakeFileList(@"C:\Games\TestGame", addonId, "shared.pak");
        target.MergeBaseAsDependentGame(addonId, addonFiles);
        target.MergeBaseAsDependentGame(addonId, addonFiles);

        Assert.Single(target.DependentGames[addonId].BaseGame.Files);
    }

    // ── MergeDependentGames ───────────────────────────────────────────────────

    [Fact]
    public void MergeDependentGames_CopiesDependentGamesFromSource()
    {
        var baseGameId = Guid.NewGuid();
        var addonId = Guid.NewGuid();

        var source = MakeFileList(@"C:\Games\TestGame", baseGameId);
        var addonFileList = MakeFileList(@"C:\Games\TestGame", addonId, "addon.pak");
        source.MergeBaseAsDependentGame(addonId, addonFileList);

        var target = MakeFileList(@"C:\Games\TestGame", baseGameId);
        target.MergeDependentGames(source);

        Assert.True(target.DependentGames.ContainsKey(addonId));
    }

    [Fact]
    public void MergeDependentGames_WithNullSource_DoesNotThrow()
    {
        var gameId = Guid.NewGuid();
        var target = MakeFileList(@"C:\Games\TestGame", gameId);

        target.MergeDependentGames(null);

        Assert.Empty(target.DependentGames);
    }

    [Fact]
    public void MergeDependentGames_WithEmptySource_LeavesTargetUnchanged()
    {
        var gameId = Guid.NewGuid();
        var target = MakeFileList(@"C:\Games\TestGame", gameId);
        var emptySource = MakeFileList(@"C:\Games\TestGame", gameId);

        target.MergeDependentGames(emptySource);

        Assert.Empty(target.DependentGames);
    }

    // ── Merge (combined) ──────────────────────────────────────────────────────

    [Fact]
    public void Merge_MergesBothBaseGameAndDependentGames()
    {
        var baseGameId = Guid.NewGuid();
        var addonId = Guid.NewGuid();

        var source = MakeFileList(@"C:\Games\TestGame", baseGameId, "new_base_file.pak");
        var addonFileList = MakeFileList(@"C:\Games\TestGame", addonId, "addon.pak");
        source.MergeBaseAsDependentGame(addonId, addonFileList);

        var target = MakeFileList(@"C:\Games\TestGame", baseGameId, "existing_base_file.pak");
        target.Merge(source);

        Assert.Equal(2, target.BaseGame.Files.Count);
        Assert.True(target.DependentGames.ContainsKey(addonId));
    }

    // ── ToFlatDistinctFileEntries ─────────────────────────────────────────────

    [Fact]
    public void ToFlatDistinctFileEntries_ReturnsBaseGameFiles()
    {
        var gameId = Guid.NewGuid();
        var list = MakeFileList(@"C:\Games\TestGame", gameId, "game.exe", "data.pak");

        var entries = list.ToFlatDistinctFileEntries().ToList();

        Assert.Equal(2, entries.Count);
    }

    [Fact]
    public void ToFlatDistinctFileEntries_IncludesDependentGameFiles()
    {
        var baseGameId = Guid.NewGuid();
        var addonId = Guid.NewGuid();
        var list = MakeFileList(@"C:\Games\TestGame", baseGameId, "game.exe");
        var addonFileList = MakeFileList(@"C:\Games\TestGame", addonId, "addon.pak");
        list.MergeBaseAsDependentGame(addonId, addonFileList);

        var entries = list.ToFlatDistinctFileEntries().ToList();

        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.EntryPath == "game.exe");
        Assert.Contains(entries, e => e.EntryPath == "addon.pak");
    }

    [Fact]
    public void ToFlatDistinctFileEntries_DeduplicatesFilesAcrossBaseAndDependentGames()
    {
        var baseGameId = Guid.NewGuid();
        var addonId = Guid.NewGuid();

        // Same file path in both base game and addon
        var list = MakeFileList(@"C:\Games\TestGame", baseGameId, "shared.cfg");
        var addonFileList = MakeFileList(@"C:\Games\TestGame", addonId, "shared.cfg", "addon.pak");
        list.MergeBaseAsDependentGame(addonId, addonFileList);

        var entries = list.ToFlatDistinctFileEntries().ToList();

        // shared.cfg should appear only once, addon.pak once
        Assert.Equal(2, entries.Count);
        Assert.Single(entries.Where(e => e.EntryPath.Equals("shared.cfg", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void ToFlatDistinctFileEntries_BaseGameFileWinsOverDependentOnDuplicate()
    {
        var baseGameId = Guid.NewGuid();
        var addonId = Guid.NewGuid();
        const string sharedPath = "shared.cfg";
        const string baseLocalPath = @"C:\base\shared.cfg";
        const string addonLocalPath = @"C:\addon\shared.cfg";

        var list = new GameInstallationFileList(@"C:\Games\TestGame", baseGameId);
        list.BaseGame.AddFile(new GameInstallationFileListEntry.FileEntry
        {
            EntryPath = sharedPath,
            LocalPath = baseLocalPath
        });

        var addonFileList = new GameInstallationFileList(@"C:\Games\TestGame", addonId);
        addonFileList.BaseGame.AddFile(new GameInstallationFileListEntry.FileEntry
        {
            EntryPath = sharedPath,
            LocalPath = addonLocalPath
        });
        list.MergeBaseAsDependentGame(addonId, addonFileList);

        var entries = list.ToFlatDistinctFileEntries().ToList();

        // GroupBy + First means the base game's entry wins
        var entry = Assert.Single(entries.Where(e => e.EntryPath == sharedPath));
        Assert.Equal(baseLocalPath, entry.LocalPath);
    }

    [Fact]
    public void ToFlatDistinctFileEntries_WithMultipleDependentGames_ReturnsAllFiles()
    {
        var baseGameId = Guid.NewGuid();
        var addon1Id = Guid.NewGuid();
        var addon2Id = Guid.NewGuid();

        var list = MakeFileList(@"C:\Games\TestGame", baseGameId, "base.exe");
        list.MergeBaseAsDependentGame(addon1Id, MakeFileList(@"C:\Games\TestGame", addon1Id, "addon1.pak"));
        list.MergeBaseAsDependentGame(addon2Id, MakeFileList(@"C:\Games\TestGame", addon2Id, "addon2.pak"));

        var entries = list.ToFlatDistinctFileEntries().ToList();

        Assert.Equal(3, entries.Count);
    }

    [Fact]
    public void Empty_ReturnsInstanceWithNullInstallDirectory()
    {
        var empty = GameInstallationFileList.Empty;

        Assert.Null(empty.InstallDirectory);
        Assert.Empty(empty.DependentGames);
    }
}
