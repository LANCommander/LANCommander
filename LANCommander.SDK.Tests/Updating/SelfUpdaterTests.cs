using System.IO.Compression;
using LANCommander.SDK.Updating;

namespace LANCommander.SDK.Tests.Updating;

public sealed class SelfUpdaterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "LANCommander.SelfUpdaterTests", Guid.NewGuid().ToString("N"));
    private readonly string _install;
    private readonly string _updates;

    public SelfUpdaterTests()
    {
        _install = Path.Combine(_root, "Install");
        _updates = Path.Combine(_root, "Data", "Updates");

        Directory.CreateDirectory(_install);
        Directory.CreateDirectory(_updates);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Best effort
        }
    }

    [Fact]
    public void Apply_ReplacesFiles_AddsNewOnes_AndKeepsUnlistedOnes()
    {
        WriteInstalled("App.exe", "old app");
        WriteInstalled("Data/Libraries/Library.dll", "old library");
        WriteInstalled("Data/Settings.yml", "user settings");

        var journal = Stage(("App.exe", "new app"), ("Data/Libraries/Library.dll", "new library"), ("Data/Libraries/Added.dll", "added"));

        SelfUpdater.Apply(journal);

        Assert.Equal(UpdateState.Applied, UpdateJournal.Load(_updates)!.State);
        Assert.Equal("new app", ReadInstalled("App.exe"));
        Assert.Equal("new library", ReadInstalled("Data/Libraries/Library.dll"));
        Assert.Equal("added", ReadInstalled("Data/Libraries/Added.dll"));
        Assert.Equal("user settings", ReadInstalled("Data/Settings.yml"));
        Assert.Equal("old app", ReadInstalled("App.exe" + UpdateJournal.BackupSuffix));
    }

    [Fact]
    public void Apply_ReportsProgress_ForEveryFile()
    {
        WriteInstalled("App.exe", "old app");

        var journal = Stage(("App.exe", "new app"), ("Added.dll", "added"), ("Data/Other.dll", "other"));
        var reports = new List<(int Done, int Total)>();

        SelfUpdater.Apply(journal, progress: (done, total) => reports.Add((done, total)));

        Assert.Equal([(0, 3), (1, 3), (2, 3), (3, 3)], reports);
    }

    [Fact]
    public void ConfirmStartup_ReturnsTheUpdate_OnlyTheFirstTime()
    {
        WriteInstalled("App.exe", "old app");

        var journal = Stage(("App.exe", "new app"));

        journal.PreviousVersion = "1.0.0";

        SelfUpdater.Apply(journal);

        var confirmed = SelfUpdater.ConfirmStartup(_updates);

        Assert.NotNull(confirmed);
        Assert.Equal("2.0.0", confirmed.Version);
        Assert.Equal("1.0.0", confirmed.PreviousVersion);
        Assert.Null(SelfUpdater.ConfirmStartup(_updates));
    }

    [Fact]
    public void UpdateResult_IsReadOnce()
    {
        new UpdateResult { Version = "2.0.0", PreviousVersion = "1.0.0", Error = "It broke." }.Save(_updates);

        var result = UpdateResult.Take(_updates);

        Assert.NotNull(result);
        Assert.Equal("It broke.", result.Error);
        Assert.Null(UpdateResult.Take(_updates));
    }

    [Fact]
    public void Complete_DeletesBackups_AndTheJournal()
    {
        WriteInstalled("App.exe", "old app");

        var journal = Stage(("App.exe", "new app"));

        SelfUpdater.Apply(journal);

        Assert.True(SelfUpdater.Complete(journal));

        Assert.False(File.Exists(InstalledPath("App.exe" + UpdateJournal.BackupSuffix)));
        Assert.False(UpdateJournal.Exists(_updates));
        Assert.False(Directory.Exists(journal.StagingDirectory));
        Assert.Equal("new app", ReadInstalled("App.exe"));
    }

    [Fact]
    public void Rollback_RestoresPreviousFiles_AndRemovesAddedOnes()
    {
        WriteInstalled("App.exe", "old app");

        var journal = Stage(("App.exe", "new app"), ("Added.dll", "added"));

        SelfUpdater.Apply(journal);

        Assert.True(SelfUpdater.Rollback(journal));

        Assert.Equal("old app", ReadInstalled("App.exe"));
        Assert.False(File.Exists(InstalledPath("Added.dll")));
        Assert.Empty(Directory.GetFiles(_install, "*" + UpdateJournal.BackupSuffix, SearchOption.AllDirectories));
        Assert.Equal(UpdateState.RolledBack, UpdateJournal.Load(_updates)!.State);
    }

    [Fact]
    public void Apply_RollsBack_WhenAFileCannotBeMoved()
    {
        WriteInstalled("App.exe", "old app");
        WriteInstalled("Library.dll", "old library");

        // A directory where the update has a file stops the move into place
        Directory.CreateDirectory(InstalledPath("Blocked.dll"));

        var journal = Stage(("App.exe", "new app"), ("Library.dll", "new library"), ("Blocked.dll", "blocked"));

        Assert.ThrowsAny<IOException>(() => SelfUpdater.Apply(journal));

        Assert.Equal("old app", ReadInstalled("App.exe"));
        Assert.Equal("old library", ReadInstalled("Library.dll"));
        Assert.Equal(UpdateState.RolledBack, UpdateJournal.Load(_updates)!.State);
    }

    [Fact]
    public void Recover_RollsBackAnInterruptedSwap_AndAsksForARestart()
    {
        WriteInstalled("App.exe", "old app");
        WriteInstalled("Library.dll", "old library");

        var journal = Stage(("App.exe", "new app"), ("Library.dll", "new library"));

        // The process died after moving App.exe aside and starting to move its replacement in
        journal.State = UpdateState.Applying;
        journal.Save();

        using (var log = UpdateOperationLog.Open(journal.OperationsPath))
        {
            log.Append(UpdateOperationKind.Backup, "App.exe");
            File.Move(InstalledPath("App.exe"), InstalledPath("App.exe") + UpdateJournal.BackupSuffix);

            log.Append(UpdateOperationKind.Install, "App.exe");
            File.WriteAllText(InstalledPath("App.exe"), "partial copy");
        }

        Assert.Equal(UpdateRecoveryAction.RestartRequired, SelfUpdater.Recover(_updates));

        Assert.Equal("old app", ReadInstalled("App.exe"));
        Assert.Equal("old library", ReadInstalled("Library.dll"));

        // The next start clears the rolled back update away
        Assert.Equal(UpdateRecoveryAction.None, SelfUpdater.Recover(_updates));
        Assert.False(UpdateJournal.Exists(_updates));
    }

    [Fact]
    public void Apply_OfABundle_MovesAsideFilesTheUpdateNoLongerHas()
    {
        WriteInstalled("Contents/MacOS/App", "old app");
        WriteInstalled("Contents/Resources/Removed.icns", "removed");

        var journal = Stage(new UpdateInstallation(_install, InstalledPath("Contents/MacOS/App"), UpdatePackageKind.Archive, IsBundle: true),
            ("LANCommander.Launcher.app/Contents/MacOS/App", "new app"));

        SelfUpdater.Apply(journal);

        Assert.False(File.Exists(InstalledPath("Contents/Resources/Removed.icns")));

        SelfUpdater.Rollback(journal);

        Assert.Equal("removed", ReadInstalled("Contents/Resources/Removed.icns"));
        Assert.Equal("old app", ReadInstalled("Contents/MacOS/App"));
    }

    [Fact]
    public async Task ConfirmStartup_FinishesAnAppliedUpdate()
    {
        WriteInstalled("App.exe", "old app");

        var journal = Stage(("App.exe", "new app"));

        journal.PreviousProcessId = Environment.ProcessId;

        SelfUpdater.Apply(journal);

        await SelfUpdater.ConfirmStartupAsync(_updates);

        Assert.False(UpdateJournal.Exists(_updates));
        Assert.False(File.Exists(InstalledPath("App.exe" + UpdateJournal.BackupSuffix)));
        Assert.Equal("new app", ReadInstalled("App.exe"));
    }

    [Fact]
    public void Apply_ReplacesAFileThatIsOpen()
    {
        WriteInstalled("App.exe", "old app");

        var journal = Stage(("App.exe", "new app"));

        // Stands in for the running executable, which can be renamed but not overwritten or deleted
        using (new FileStream(InstalledPath("App.exe"), FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
        {
            SelfUpdater.Apply(journal);

            Assert.Equal("new app", ReadInstalled("App.exe"));
        }

        Assert.True(SelfUpdater.Complete(journal));
        Assert.False(File.Exists(InstalledPath("App.exe" + UpdateJournal.BackupSuffix)));
    }

    [Fact]
    public void Begin_FinishesAnEarlierUpdate()
    {
        WriteInstalled("App.exe", "old app");

        var first = Stage(("App.exe", "new app"));

        SelfUpdater.Apply(first);

        first.State = UpdateState.Confirmed;
        first.Save();

        var second = SelfUpdater.Begin(_updates, "3.0.0", Installation());

        Assert.Equal(UpdateState.Staged, second.State);
        Assert.False(File.Exists(InstalledPath("App.exe" + UpdateJournal.BackupSuffix)));
        Assert.False(File.Exists(first.OperationsPath));
    }

    [Fact]
    public void Begin_RefusesWhileAnUpdateIsBeingApplied()
    {
        WriteInstalled("App.exe", "old app");

        var journal = Stage(("App.exe", "new app"));

        SelfUpdater.Apply(journal);

        Assert.Throws<InvalidOperationException>(() => SelfUpdater.Begin(_updates, "3.0.0", Installation()));
    }

    [Fact]
    public void ExtractArchive_RejectsEntriesOutsideTheStagingDirectory()
    {
        var archive = CreateArchive(("../Escaped.dll", "escaped"));

        Assert.Throws<InvalidDataException>(() => UpdatePackage.ExtractArchive(archive, Path.Combine(_updates, "Staging"), bundle: false));

        Assert.False(File.Exists(Path.Combine(_updates, "Escaped.dll")));
    }

    [Fact]
    public void ExtractArchive_OfABundle_ExtractsOnlyTheBundleContents()
    {
        var archive = CreateArchive(
            ("LANCommander.Launcher.app/Contents/MacOS/LANCommander.Launcher", "app"),
            ("LANCommander.Launcher.app/Contents/Info.plist", "plist"),
            ("ReadMe.txt", "beside the bundle"));

        var staging = Path.Combine(_updates, "Staging");

        UpdatePackage.ExtractArchive(archive, staging, bundle: true);

        Assert.Equal("app", File.ReadAllText(Path.Combine(staging, "Contents", "MacOS", "LANCommander.Launcher")));
        Assert.Equal("plist", File.ReadAllText(Path.Combine(staging, "Contents", "Info.plist")));
        Assert.False(File.Exists(Path.Combine(staging, "ReadMe.txt")));
    }

    [Fact]
    public async Task Verify_ComparesTheChecksum()
    {
        var path = Path.Combine(_root, "Package.zip");

        await File.WriteAllTextAsync(path, "package");

        var hash = await UpdatePackage.ComputeSha256Async(path);

        Assert.True(await UpdatePackage.VerifyAsync(path, hash.ToUpperInvariant()));
        Assert.True(await UpdatePackage.VerifyAsync(path, null));
        Assert.False(await UpdatePackage.VerifyAsync(path, new string('0', 64)));
    }

    private UpdateInstallation Installation() =>
        new(_install, InstalledPath("App.exe"), UpdatePackageKind.Archive, IsBundle: false);

    private UpdateJournal Stage(params (string Path, string Content)[] files) => Stage(Installation(), files);

    private UpdateJournal Stage(UpdateInstallation installation, params (string Path, string Content)[] files)
    {
        var journal = SelfUpdater.Begin(_updates, "2.0.0", installation);

        UpdatePackage.Stage(journal, CreateArchive(files), UpdatePackageKind.Archive);
        journal.Save();

        return journal;
    }

    private string CreateArchive(params (string Path, string Content)[] files)
    {
        var path = Path.Combine(_root, $"{Guid.NewGuid():N}.zip");

        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);

        foreach (var (entryPath, content) in files)
        {
            using var writer = new StreamWriter(archive.CreateEntry(entryPath).Open());

            writer.Write(content);
        }

        return path;
    }

    private string InstalledPath(string relativePath) => Path.Combine(_install, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private void WriteInstalled(string relativePath, string content)
    {
        var path = InstalledPath(relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private string ReadInstalled(string relativePath) => File.ReadAllText(InstalledPath(relativePath));
}
