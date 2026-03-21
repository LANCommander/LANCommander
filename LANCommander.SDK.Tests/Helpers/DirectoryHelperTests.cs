using LANCommander.SDK.Helpers;

namespace LANCommander.SDK.Tests.Helpers;

public class DirectoryHelperTests : IDisposable
{
    private readonly string _tempDir;

    public DirectoryHelperTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"lc-dir-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private string MakeDir(params string[] parts)
    {
        var path = Path.Combine(new[] { _tempDir }.Concat(parts).ToArray());
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WriteFile(string dir, string name, string content = "content")
    {
        File.WriteAllText(Path.Combine(dir, name), content);
    }

    // ── DeleteEmptyDirectories ────────────────────────────────────────────────

    [Fact]
    public void DeleteEmptyDirectories_EmptyDirectory_IsDeleted()
    {
        var dir = MakeDir("empty");

        DirectoryHelper.DeleteEmptyDirectories(dir);

        Assert.False(Directory.Exists(dir));
    }

    [Fact]
    public void DeleteEmptyDirectories_NonEmptyDirectory_IsNotDeleted()
    {
        var dir = MakeDir("nonempty");
        WriteFile(dir, "file.txt");

        DirectoryHelper.DeleteEmptyDirectories(dir);

        Assert.True(Directory.Exists(dir));
    }

    [Fact]
    public void DeleteEmptyDirectories_NestedEmptyDirectories_AreAllDeleted()
    {
        var parent = MakeDir("parent");
        var child = MakeDir("parent", "child");
        var grandchild = MakeDir("parent", "child", "grandchild");

        DirectoryHelper.DeleteEmptyDirectories(parent);

        Assert.False(Directory.Exists(grandchild));
        Assert.False(Directory.Exists(child));
        Assert.False(Directory.Exists(parent));
    }

    [Fact]
    public void DeleteEmptyDirectories_WhenSiblingHasFile_EmptySubdirIsDeletedParentIsNot()
    {
        var parent = MakeDir("mixed");
        var withFile = MakeDir("mixed", "withfile");
        WriteFile(withFile, "file.txt");
        var empty = MakeDir("mixed", "empty");

        DirectoryHelper.DeleteEmptyDirectories(parent);

        Assert.True(Directory.Exists(withFile));
        Assert.True(Directory.Exists(parent));
        Assert.False(Directory.Exists(empty));
    }

    [Fact]
    public void DeleteEmptyDirectories_WhenDirectoryDoesNotExist_DoesNotThrow()
    {
        var path = Path.Combine(_tempDir, "nonexistent");

        var ex = Record.Exception(() => DirectoryHelper.DeleteEmptyDirectories(path));

        Assert.Null(ex);
    }

    [Fact]
    public void DeleteEmptyDirectories_WhenPathIsNull_DoesNotThrow()
    {
        var ex = Record.Exception(() => DirectoryHelper.DeleteEmptyDirectories(null));

        Assert.Null(ex);
    }

    [Fact]
    public void DeleteEmptyDirectories_WhenPathIsWhitespace_DoesNotThrow()
    {
        var ex = Record.Exception(() => DirectoryHelper.DeleteEmptyDirectories("   "));

        Assert.Null(ex);
    }

    // ── IsDirectoryWritable ───────────────────────────────────────────────────

    [Fact]
    public void IsDirectoryWritable_ExistingWritableDirectory_ReturnsTrue()
    {
        var result = DirectoryHelper.IsDirectoryWritable(_tempDir);

        Assert.True(result);
    }

    [Fact]
    public void IsDirectoryWritable_NonExistentDirectory_CreatesItAndReturnsTrue()
    {
        var path = Path.Combine(_tempDir, "new-writable");

        var result = DirectoryHelper.IsDirectoryWritable(path);

        Assert.True(result);
        Assert.True(Directory.Exists(path));
    }

    [Fact]
    public void IsDirectoryWritable_LeavesNoProbeFile()
    {
        DirectoryHelper.IsDirectoryWritable(_tempDir);

        var probeFiles = Directory.GetFiles(_tempDir, ".writetest.*.tmp");
        Assert.Empty(probeFiles);
    }

    // ── MoveContents: argument validation ─────────────────────────────────────

    [Fact]
    public void MoveContents_NullSource_ThrowsArgumentException()
    {
        var dest = MakeDir("dest-null-src");

        Assert.Throws<ArgumentException>(() => DirectoryHelper.MoveContents(null, dest));
    }

    [Fact]
    public void MoveContents_EmptySource_ThrowsArgumentException()
    {
        var dest = MakeDir("dest-empty-src");

        Assert.Throws<ArgumentException>(() => DirectoryHelper.MoveContents("", dest));
    }

    [Fact]
    public void MoveContents_NullDestination_ThrowsArgumentException()
    {
        var source = MakeDir("src-null-dest");

        Assert.Throws<ArgumentException>(() => DirectoryHelper.MoveContents(source, null));
    }

    [Fact]
    public void MoveContents_EmptyDestination_ThrowsArgumentException()
    {
        var source = MakeDir("src-empty-dest");

        Assert.Throws<ArgumentException>(() => DirectoryHelper.MoveContents(source, ""));
    }

    [Fact]
    public void MoveContents_NonExistentSource_ThrowsDirectoryNotFoundException()
    {
        var source = Path.Combine(_tempDir, "no-such-dir");
        var dest = MakeDir("dest-no-src");

        Assert.Throws<DirectoryNotFoundException>(() => DirectoryHelper.MoveContents(source, dest));
    }

    // ── MoveContents: functional behaviour ───────────────────────────────────

    [Fact]
    public void MoveContents_MovesFilesToDestination()
    {
        var source = MakeDir("src-move");
        var dest = Path.Combine(_tempDir, "dest-move");
        WriteFile(source, "game.exe", "binary");
        WriteFile(source, "data.pak", "data");

        DirectoryHelper.MoveContents(source, dest);

        Assert.True(File.Exists(Path.Combine(dest, "game.exe")));
        Assert.True(File.Exists(Path.Combine(dest, "data.pak")));
    }

    [Fact]
    public void MoveContents_PreservesFileContent()
    {
        var source = MakeDir("src-content");
        var dest = Path.Combine(_tempDir, "dest-content");
        WriteFile(source, "readme.txt", "hello content");

        DirectoryHelper.MoveContents(source, dest);

        Assert.Equal("hello content", File.ReadAllText(Path.Combine(dest, "readme.txt")));
    }

    [Fact]
    public void MoveContents_SourceFilesAreRemovedAfterMove()
    {
        var source = MakeDir("src-rm");
        var dest = Path.Combine(_tempDir, "dest-rm");
        WriteFile(source, "file.txt");

        DirectoryHelper.MoveContents(source, dest);

        Assert.False(File.Exists(Path.Combine(source, "file.txt")));
    }

    [Fact]
    public void MoveContents_CreatesDestinationDirectoryIfNotExists()
    {
        var source = MakeDir("src-newdest");
        WriteFile(source, "file.txt");
        var dest = Path.Combine(_tempDir, "brand-new-dest");

        DirectoryHelper.MoveContents(source, dest);

        Assert.True(Directory.Exists(dest));
    }

    [Fact]
    public void MoveContents_MovesSubdirectoryContentsRecursively()
    {
        var source = MakeDir("src-sub");
        var subdir = MakeDir("src-sub", "subdir");
        WriteFile(subdir, "nested.txt", "nested");
        var dest = Path.Combine(_tempDir, "dest-sub");

        DirectoryHelper.MoveContents(source, dest);

        Assert.True(File.Exists(Path.Combine(dest, "subdir", "nested.txt")));
        Assert.Equal("nested", File.ReadAllText(Path.Combine(dest, "subdir", "nested.txt")));
    }

    [Fact]
    public void MoveContents_WhenDestinationFileExists_BacksUpConflictingFile()
    {
        var source = MakeDir("src-conflict");
        var dest = MakeDir("dest-conflict");
        WriteFile(source, "game.exe", "new-version");
        WriteFile(dest, "game.exe", "old-version");

        DirectoryHelper.MoveContents(source, dest);

        Assert.Equal("new-version", File.ReadAllText(Path.Combine(dest, "game.exe")));
        Assert.True(File.Exists(Path.Combine(dest, "game.exe.bak")));
        Assert.Equal("old-version", File.ReadAllText(Path.Combine(dest, "game.exe.bak")));
    }

    [Fact]
    public void MoveContents_WhenBakFileAlreadyExists_ChainsBakExtensions()
    {
        var source = MakeDir("src-doublebak");
        var dest = MakeDir("dest-doublebak");
        WriteFile(source, "game.exe", "newest");
        WriteFile(dest, "game.exe", "current");
        WriteFile(dest, "game.exe.bak", "previous");

        DirectoryHelper.MoveContents(source, dest);

        Assert.Equal("newest", File.ReadAllText(Path.Combine(dest, "game.exe")));
        Assert.Equal("current", File.ReadAllText(Path.Combine(dest, "game.exe.bak")));
        Assert.True(File.Exists(Path.Combine(dest, "game.exe.bak.bak")));
        Assert.Equal("previous", File.ReadAllText(Path.Combine(dest, "game.exe.bak.bak")));
    }
}
