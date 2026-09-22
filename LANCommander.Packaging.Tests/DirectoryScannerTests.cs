using LANCommander.Packaging.Analysis;
using Shouldly;

namespace LANCommander.Packaging.Tests;

public class DirectoryScannerTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"lancommander-scan-{Guid.NewGuid():N}");

    public DirectoryScannerTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private string Write(string relativePath, string contents)
    {
        var path = Path.Combine(_root, relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);

        return path;
    }

    [Fact]
    public void CaptureRecordsEveryFileBeneathTheRoot()
    {
        Write("game.exe", "original");
        Write(Path.Combine("Data", "levels.dat"), "levels");

        var snapshot = DirectoryScanner.Capture(_root);

        snapshot.RootExists.ShouldBeTrue();
        snapshot.FileCount.ShouldBe(2);
    }

    [Fact]
    public void CaptureIncludesHiddenFiles()
    {
        var path = Write("config.ini", "[settings]");

        File.SetAttributes(path, FileAttributes.Hidden);

        DirectoryScanner.Capture(_root).Files.Keys.ShouldContain(path);
    }

    [Fact]
    public void CaptureOfAMissingDirectoryIsDistinctFromAnEmptyOne()
    {
        var missing = DirectoryScanner.Capture(Path.Combine(_root, "not-here"));

        missing.RootExists.ShouldBeFalse();
        missing.FileCount.ShouldBe(0);
    }

    [Fact]
    public void DiffFindsAFileDroppedInByHand()
    {
        Write("game.exe", "original");

        var before = DirectoryScanner.Capture(_root);

        var patch = Write("ddraw.dll", "wrapper");

        var diff = DirectoryScanner.Diff(before, DirectoryScanner.Capture(_root));

        diff.Added.ShouldHaveSingleItem().Path.ShouldBe(patch);
        diff.Added[0].RelativePath.ShouldBe("ddraw.dll");
        diff.Modified.ShouldBeEmpty();
        diff.Removed.ShouldBeEmpty();
    }

    [Fact]
    public void DiffFindsAPatchedExecutable()
    {
        // The no-CD patch case: same file, different contents and a different length.
        var executable = Write("game.exe", "original");

        var before = DirectoryScanner.Capture(_root);

        File.WriteAllText(executable, "patched executable");

        var diff = DirectoryScanner.Diff(before, DirectoryScanner.Capture(_root));

        var change = diff.Modified.ShouldHaveSingleItem();

        change.Path.ShouldBe(executable);
        change.LengthDelta.ShouldBeGreaterThan(0);
        diff.Added.ShouldBeEmpty();
    }

    [Fact]
    public void DiffFindsASameLengthEditFromItsTimestamp()
    {
        // A config edit that happens to preserve the file's length. Length alone would miss it,
        // which is why the fingerprint carries the write time as well.
        var config = Write("config.ini", "windowed=0");

        var before = DirectoryScanner.Capture(_root);

        File.WriteAllText(config, "windowed=1");
        File.SetLastWriteTimeUtc(config, DateTime.UtcNow.AddMinutes(1));

        DirectoryScanner.Diff(before, DirectoryScanner.Capture(_root))
            .Modified.ShouldHaveSingleItem()
            .Path.ShouldBe(config);
    }

    [Fact]
    public void DiffIgnoresAFileThatWasOnlyRead()
    {
        // Reading must not register as a change, or every rescan after launching the game once
        // would present a list of everything the game opened.
        var executable = Write("game.exe", "original");

        var before = DirectoryScanner.Capture(_root);

        _ = File.ReadAllText(executable);

        DirectoryScanner.Diff(before, DirectoryScanner.Capture(_root)).IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void DiffFindsADeletedFile()
    {
        var junk = Write(Path.Combine("_CD", "installer.log"), "log");

        var before = DirectoryScanner.Capture(_root);

        File.Delete(junk);

        var diff = DirectoryScanner.Diff(before, DirectoryScanner.Capture(_root));

        diff.Removed.ShouldHaveSingleItem().Path.ShouldBe(junk);
        diff.Removed[0].RelativePath.ShouldBe(Path.Combine("_CD", "installer.log"));
    }

    [Fact]
    public void DiffAgainstAMissingBaselineReportsNoRemovals()
    {
        // Baselining a folder that did not exist yet, then finding it populated. Treating the
        // absent baseline's contents as deletions would read as the user wiping a tree they
        // never had.
        var before = DirectoryScanner.Capture(Path.Combine(_root, "not-here"));

        Write("game.exe", "original");

        var diff = DirectoryScanner.Diff(before, DirectoryScanner.Capture(_root));

        diff.Removed.ShouldBeEmpty();
        diff.Added.ShouldHaveSingleItem();
    }

    [Fact]
    public void DiffOrdersChangesByPath()
    {
        var before = DirectoryScanner.Capture(_root);

        Write("zebra.dat", "z");
        Write("alpha.dat", "a");
        Write("middle.dat", "m");

        var diff = DirectoryScanner.Diff(before, DirectoryScanner.Capture(_root));

        diff.Added.Select(c => c.RelativePath)
            .ShouldBe(["alpha.dat", "middle.dat", "zebra.dat"]);
    }

    [Fact]
    public void DiffCountsAcrossEveryKind()
    {
        var patched = Write("game.exe", "original");
        var deleted = Write("readme.txt", "readme");

        var before = DirectoryScanner.Capture(_root);

        File.WriteAllText(patched, "patched executable");
        File.Delete(deleted);
        Write("ddraw.dll", "wrapper");

        var diff = DirectoryScanner.Diff(before, DirectoryScanner.Capture(_root));

        diff.TotalCount.ShouldBe(3);
        diff.IsEmpty.ShouldBeFalse();
        diff.AddedOrModified.Count().ShouldBe(2);
    }
}
