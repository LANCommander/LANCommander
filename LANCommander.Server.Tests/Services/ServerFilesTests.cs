using System.Text;
using LANCommander.Server.Services.Utilities;
using Shouldly;

namespace LANCommander.Server.Tests.Services;

/// <summary>
/// A server's files are reached by paths relative to them, which can't climb out, and a local
/// server's are those in its working directory.
/// </summary>
public class ServerFilesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"lancommander-server-files-{Guid.NewGuid():N}");

    public ServerFilesTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "cfg"));
        File.WriteAllText(Path.Combine(_root, "cfg", "server.cfg"), "hostname \"LAN\"");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("/", "")]
    [InlineData("cfg", "cfg")]
    [InlineData("/cfg/", "cfg")]
    [InlineData(@"cfg\server.cfg", "cfg/server.cfg")]
    [InlineData("./cfg//./server.cfg", "cfg/server.cfg")]
    public void PathsHaveOneSpelling(string? path, string expected) =>
        ServerFilePath.Normalize(path).ShouldBe(expected);

    [Theory]
    [InlineData("..")]
    [InlineData("cfg/../../etc/passwd")]
    [InlineData(@"..\Windows")]
    [InlineData("C:/Windows")]
    [InlineData("server.cfg:stream")]
    [InlineData("bad\0name")]
    public void PathsCantLeaveTheServersFiles(string path) =>
        Should.Throw<ArgumentException>(() => ServerFilePath.Normalize(path));

    [Fact]
    public void PathsAreCombinedAndSplit()
    {
        ServerFilePath.Combine("", "server.cfg").ShouldBe("server.cfg");
        ServerFilePath.Combine("cfg/", "/server.cfg").ShouldBe("cfg/server.cfg");
        ServerFilePath.GetDirectory("cfg/server.cfg").ShouldBe("cfg");
        ServerFilePath.GetDirectory("server.cfg").ShouldBe("");
        ServerFilePath.GetName("cfg/server.cfg").ShouldBe("server.cfg");
    }

    [Fact]
    public void ALocalServerNeedsAWorkingDirectory() =>
        Should.Throw<InvalidOperationException>(() => new LocalServerFiles(""));

    [Fact]
    public async Task ALocalServersFilesAreListedReadWrittenAndDeleted()
    {
        var files = new LocalServerFiles(_root);

        files.List("").ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            f => f.Name.ShouldBe("cfg"),
            f => f.IsDirectory.ShouldBeTrue());

        var config = files.List("cfg").ShouldHaveSingleItem();
        config.Path.ShouldBe("cfg/server.cfg");
        config.Size.ShouldBe(14);

        await using (var stream = files.OpenRead("cfg/server.cfg"))
            new StreamReader(stream).ReadToEnd().ShouldBe("hostname \"LAN\"");

        // Folders it is in are created
        await files.WriteAsync("maps/custom/de_lan.bsp", new MemoryStream(Encoding.UTF8.GetBytes("map")), CancellationToken.None);
        File.ReadAllText(Path.Combine(_root, "maps", "custom", "de_lan.bsp")).ShouldBe("map");

        files.Delete("maps");
        Directory.Exists(Path.Combine(_root, "maps")).ShouldBeFalse();

        Should.Throw<InvalidOperationException>(() => files.Delete(""));
        Should.Throw<FileNotFoundException>(() => files.OpenRead("missing.cfg"));
        Should.Throw<DirectoryNotFoundException>(() => files.List("missing"));
    }

    [Fact]
    public void NothingOutsideTheWorkingDirectoryCanBeReached()
    {
        var files = new LocalServerFiles(Path.Combine(_root, "cfg"));

        Should.Throw<ArgumentException>(() => files.OpenRead("../cfg/server.cfg"));
        Should.Throw<ArgumentException>(() => files.Delete(".."));
    }
}
