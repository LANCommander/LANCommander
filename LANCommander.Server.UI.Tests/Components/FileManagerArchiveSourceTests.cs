using System.IO.Compression;
using LANCommander.SDK.Services;
using LANCommander.Server.UI.Components;

namespace LANCommander.Server.UI.Tests.Components;

public class FileManagerArchiveSourceTests
{
    sealed class StubArchiveClient(IEnumerable<ZipArchiveEntry> entries) : IArchiveClient
    {
        public Task<IEnumerable<ZipArchiveEntry>> GetContentsAsync(Guid archiveId) => Task.FromResult(entries);
    }

    static FileManagerArchiveSource CreateSource(params string[] names)
    {
        var stream = new MemoryStream();

        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var name in names)
                zip.CreateEntry(name);
        }

        stream.Position = 0;

        var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        return new FileManagerArchiveSource(new StubArchiveClient(archive.Entries.ToList()), Guid.NewGuid());
    }

    [Fact]
    public void FoldersWithoutDirectoryEntriesAreListed()
    {
        var source = CreateSource("ゲーム/起動.exe", "ゲーム/データ/セーブ.dat", "readme.txt");

        var root = source.GetDirectoryTree().Single();
        var game = Assert.Single(root.Children);

        Assert.Equal("ゲーム/", game.Path);
        Assert.Equal("ゲーム", game.Name);
        Assert.Equal("ゲーム/データ/", Assert.Single(game.Children).Path);

        Assert.Equal(new[] { "readme.txt", "ゲーム/" }, source.GetEntries().Select(e => e.Path).Order(StringComparer.Ordinal));

        source.SetCurrentPath(game);

        Assert.Equal(new[] { "ゲーム/データ/", "ゲーム/起動.exe" }, source.GetEntries().Select(e => e.Path).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ExplicitDirectoryEntriesAreNotDuplicated()
    {
        var source = CreateSource("Game/", "Game/Game.exe", "Game/Data/", "Game/Data/file.dat");

        var root = source.GetDirectoryTree().Single();
        var game = Assert.Single(root.Children);

        Assert.Equal("Game/Data/", Assert.Single(game.Children).Path);

        source.SetCurrentPath(game);

        Assert.Equal(new[] { "Game/Data/", "Game/Game.exe" }, source.GetEntries().Select(e => e.Path).Order(StringComparer.Ordinal));
    }
}
