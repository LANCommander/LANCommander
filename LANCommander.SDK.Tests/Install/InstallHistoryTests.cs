using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;
using LANCommander.SDK.Services;

namespace LANCommander.SDK.Tests.Install;

/// <summary>
/// The install history records the files each applied archive wrote, so a rollback can undo newer
/// updates and uninstall can remove the files of every update rather than just the last one.
/// </summary>
public class InstallHistoryTests : IDisposable
{
    private readonly string _installDir;
    private readonly Guid _gameId = Guid.NewGuid();

    public InstallHistoryTests()
    {
        _installDir = Path.Combine(Path.GetTempPath(), $"lc-sdk-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_installDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_installDir))
            Directory.Delete(_installDir, true);
    }

    private static InstalledArchiveFile File(string path, bool created, string crc = "AB") =>
        new() { Path = path, Crc = crc, Created = created };

    private static AppliedArchiveInfo Archive(Guid archiveId, int sortOrder) =>
        new() { ArchiveId = archiveId, VersionId = Guid.NewGuid(), Version = $"{sortOrder}.0", SortOrder = sortOrder };

    [Fact]
    public void Record_WritesHistoryAndPerArchiveFileList()
    {
        var archiveId = Guid.NewGuid();

        InstallHistoryHelper.Record(_installDir, _gameId, Archive(archiveId, 0), [File("game.exe", true), File("data/a.dat", false)]);

        var history = InstallHistoryHelper.Read(_installDir, _gameId);

        Assert.Single(history.Archives);
        Assert.Equal(archiveId, history.Archives[0].ArchiveId);
        Assert.Equal("0.0", history.Archives[0].Version);

        var files = InstallHistoryHelper.ReadArchiveFiles(_installDir, _gameId, archiveId);

        Assert.Equal(2, files.Count);
        Assert.True(files.Single(f => f.Path == "game.exe").Created);
        Assert.False(files.Single(f => f.Path == "data/a.dat").Created);
    }

    [Fact]
    public void Record_FileListIsUnionOfAllArchivesWithLaterCrcWinning()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        InstallHistoryHelper.Record(_installDir, _gameId, Archive(first, 0), [File("game.exe", true, "1"), File("a.dat", true, "1")]);
        InstallHistoryHelper.Record(_installDir, _gameId, Archive(second, 1), [File("a.dat", false, "2"), File("b.dat", true, "2")]);

        var lines = System.IO.File.ReadAllLines(GameClient.GetMetadataFilePath(_installDir, _gameId, "FileList.txt"));

        Assert.Equal(["game.exe | 1", "a.dat | 2", "b.dat | 2"], lines);
    }

    [Fact]
    public void Record_ReapplyingAnArchiveKeepsItsCreatedFlags()
    {
        var archiveId = Guid.NewGuid();

        InstallHistoryHelper.Record(_installDir, _gameId, Archive(archiveId, 1), [File("new.dat", true)]);

        // Extracting it again finds the file already there
        InstallHistoryHelper.Record(_installDir, _gameId, Archive(archiveId, 1), [File("new.dat", false)]);

        Assert.True(InstallHistoryHelper.ReadArchiveFiles(_installDir, _gameId, archiveId).Single().Created);
        Assert.Single(InstallHistoryHelper.Read(_installDir, _gameId).Archives);
    }

    [Fact]
    public void Remove_ForgetsArchivesAndRebuildsFileList()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        InstallHistoryHelper.Record(_installDir, _gameId, Archive(first, 0), [File("game.exe", true)]);
        InstallHistoryHelper.Record(_installDir, _gameId, Archive(second, 1), [File("b.dat", true)]);

        InstallHistoryHelper.Remove(_installDir, _gameId, [second]);

        Assert.Equal([first], InstallHistoryHelper.Read(_installDir, _gameId).Archives.Select(a => a.ArchiveId));
        Assert.False(System.IO.File.Exists(InstallHistoryHelper.GetArchiveFileListPath(_installDir, _gameId, second)));
        Assert.Equal(["game.exe | AB"], System.IO.File.ReadAllLines(GameClient.GetMetadataFilePath(_installDir, _gameId, "FileList.txt")));
    }

    [Fact]
    public void ParseArchiveFiles_ReadsLegacyLinesWithoutFlags()
    {
        var files = InstallHistoryHelper.ParseArchiveFiles(["dir\\game.exe | 1F", "", "a.dat | 2 | C"]);

        Assert.Equal(2, files.Count);
        Assert.Equal("dir/game.exe", files[0].Path);
        Assert.False(files[0].Created);
        Assert.True(files[1].Created);
    }
}

public class RollbackPlannerTests
{
    private readonly Dictionary<Guid, List<InstalledArchiveFile>> _files = new();
    private readonly InstallHistory _history = new();

    private Guid Applied(int sortOrder, params (string Path, bool Created)[] files)
    {
        var archiveId = Guid.NewGuid();

        _history.Archives.Add(new InstallHistoryEntry { ArchiveId = archiveId, SortOrder = sortOrder, AppliedOn = DateTime.UtcNow.AddMinutes(sortOrder) });
        _files[archiveId] = files.Select(f => new InstalledArchiveFile { Path = f.Path, Crc = "0", Created = f.Created }).ToList();

        return archiveId;
    }

    private RollbackPlan Plan(Guid targetArchiveId, int targetSortOrder, params string[] targetEntries)
        => RollbackPlanner.Plan(_history, id => _files.TryGetValue(id, out var f) ? f : [], targetArchiveId, targetSortOrder, targetEntries);

    [Fact]
    public void DeletesFilesCreatedByNewerUpdates()
    {
        var v1 = Applied(0, ("game.exe", true), ("a.dat", true));
        var v2 = Applied(1, ("new2.dat", true));
        var v3 = Applied(2, ("new3.dat", true), ("maps/", true));

        var plan = Plan(v1, 0, "game.exe", "a.dat");

        Assert.Equal(["new2.dat", "new3.dat"], plan.Delete.OrderBy(p => p));
        Assert.Empty(plan.Restore);
        Assert.Equal([v2, v3], plan.RemoveHistory);
    }

    [Fact]
    public void RestoresOverwrittenFilesMissingFromTargetFromNewestKeptArchive()
    {
        var v1 = Applied(0, ("game.exe", true), ("a.dat", true), ("b.dat", true));
        var v2 = Applied(1, ("b.dat", false));
        Applied(2, ("a.dat", false), ("b.dat", false));

        // The target is a delta that only ships game.exe, so a.dat comes from v1 and b.dat from v2
        var target = Guid.NewGuid();
        var plan = Plan(target, 1, "game.exe");

        Assert.Empty(plan.Delete);
        Assert.Equal(v1, plan.Restore["a.dat"]);
        Assert.Equal(v2, plan.Restore["b.dat"]);
    }

    [Fact]
    public void LeavesFilesTheTargetArchiveRewrites()
    {
        var v1 = Applied(0, ("game.exe", true));
        Applied(1, ("game.exe", false), ("extra.dat", true));

        var plan = Plan(v1, 0, "game.exe", "extra.dat");

        Assert.Empty(plan.Delete);
        Assert.Empty(plan.Restore);
    }

    [Fact]
    public void RestoresCreatedFileAKeptArchiveAlsoProvides()
    {
        var v1 = Applied(0, ("game.exe", true));
        var v2 = Applied(1, ("shared.dat", true));

        // v3 re-created a file v2 had added and something later removed
        Applied(2, ("shared.dat", true));

        var plan = Plan(Guid.NewGuid(), 1, "game.exe");

        Assert.Empty(plan.Delete);
        Assert.Equal(v2, plan.Restore["shared.dat"]);
    }

    [Fact]
    public void LeavesOverwrittenFilesNoArchiveOwns()
    {
        var v1 = Applied(0, ("game.exe", true));

        // The update overwrote a file the user made, which no archive provides
        Applied(1, ("user.cfg", false));

        var plan = Plan(v1, 0, "game.exe");

        Assert.Empty(plan.Delete);
        Assert.Empty(plan.Restore);
    }

    [Fact]
    public void MultiStepRollbackUndoesEveryNewerUpdate()
    {
        var v1 = Applied(0, ("game.exe", true), ("a.dat", true));
        Applied(1, ("a.dat", false), ("b.dat", true));
        Applied(2, ("b.dat", false), ("c.dat", true));

        var plan = Plan(v1, 0, "game.exe", "a.dat");

        // a.dat comes back when v1's archive is extracted again
        Assert.Equal(["b.dat", "c.dat"], plan.Delete.OrderBy(p => p));
        Assert.Empty(plan.Restore);
    }

    [Fact]
    public void ServerContentsFallbackDeletesOnlyFilesUniqueToNewerArchives()
    {
        var plan = RollbackPlanner.PlanFromServerContents(
            newerArchiveEntries: [["game.exe", "new.dat", "maps/"], ["other.dat"]],
            keptArchiveEntries: [["game.exe"]]);

        Assert.Equal(["new.dat", "other.dat"], plan.Delete.OrderBy(p => p));
        Assert.Empty(plan.Restore);
    }
}

public class RedistributableSyncPlannerTests
{
    [Fact]
    public void InstallsMissingAndUninstallsOnesOnlyThePreviousVersionListed()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var installed = new HashSet<Guid> { a, b };

        var plan = RedistributableSyncPlanner.Plan(previous: [a, b], target: [b, c], installed.Contains);

        Assert.Equal([c], plan.Install);
        Assert.Equal([a], plan.Uninstall);
    }

    [Fact]
    public void LeavesRedistributablesThatAreNotInstalledAlone()
    {
        var a = Guid.NewGuid();

        var plan = RedistributableSyncPlanner.Plan(previous: [a], target: [], _ => false);

        Assert.Empty(plan.Install);
        Assert.Empty(plan.Uninstall);
    }
}

public class InstalledVersionIdTests : IDisposable
{
    private readonly string _installDir = Path.Combine(Path.GetTempPath(), $"lc-sdk-tests-{Guid.NewGuid()}");
    private readonly Guid _gameId = Guid.NewGuid();

    public void Dispose()
    {
        if (Directory.Exists(_installDir))
            Directory.Delete(_installDir, true);
    }

    [Fact]
    public async Task ReadsTheManifestsVersionFirst()
    {
        var versionId = Guid.NewGuid();

        InstallHistoryHelper.Record(_installDir, _gameId, new AppliedArchiveInfo { ArchiveId = Guid.NewGuid(), VersionId = Guid.NewGuid() }, []);
        await ManifestHelper.WriteAsync(new Models.Manifest.Game { Id = _gameId, Title = "Game", VersionId = versionId }, _installDir);

        Assert.Equal(versionId, GameClient.GetInstalledVersionId(_installDir, _gameId));
    }

    [Fact]
    public void FallsBackToTheNewestHistoryEntry()
    {
        var versionId = Guid.NewGuid();

        InstallHistoryHelper.Record(_installDir, _gameId, new AppliedArchiveInfo { ArchiveId = Guid.NewGuid(), VersionId = versionId }, []);

        Assert.Equal(versionId, GameClient.GetInstalledVersionId(_installDir, _gameId));
    }

    [Fact]
    public void IsNullForInstallsWithoutVersions()
    {
        Assert.Null(GameClient.GetInstalledVersionId(_installDir, _gameId));
    }
}
