using System.IO.Compression;
using System.Text;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using LANCommander.Server.Plugins;
using LANCommander.Server.Services;
using Shouldly;

namespace LANCommander.Server.Tests.Services;

/// <summary>
/// <see cref="IPackageExporter"/> as plugins use it: describing a record, and writing a package of it
/// that the server's own importer reads back.
/// </summary>
[Collection("Application")]
public class PackageExporterTests(ApplicationFixture fixture) : BaseTest(fixture)
{
    [Fact]
    public async Task ARecordIsDescribedWithItsChildRecords()
    {
        var (gameId, archiveId, archiveBytes) = await ImportGameAsync();

        var contents = await GetService<IPackageExporter>().DescribeAsync(ImportExportRecordType.Game, gameId);

        contents.RecordId.ShouldBe(gameId);
        contents.Name.ShouldBe($"Exported By A Plugin {gameId:N}");

        var archive = contents.Items.Single(i => i.Type == ImportExportRecordType.Archive);

        archive.Id.ShouldBe(archiveId);
        archive.Size.ShouldBe(archiveBytes.Length);
        archive.UpdatedOn.ShouldNotBeNull();
    }

    [Fact]
    public async Task APackageLeavesOutTheChildRecordsItsToldTo()
    {
        var (gameId, archiveId, archiveBytes) = await ImportGameAsync();
        var exporter = GetService<IPackageExporter>();

        var whole = await ExportAsync(exporter, gameId);
        var withoutArchive = await ExportAsync(exporter, gameId, archiveId);

        using (var zip = new ZipArchive(new MemoryStream(whole)))
        {
            zip.GetEntry($"Archives/{archiveId}").ShouldNotBeNull();
            ManifestOf(zip).ShouldContain(archiveId.ToString());
        }

        using (var zip = new ZipArchive(new MemoryStream(withoutArchive)))
        {
            zip.Entries.ShouldNotContain(e => e.FullName.StartsWith("Archives/"));
            ManifestOf(zip).ShouldContain(gameId.ToString());
            ManifestOf(zip).ShouldNotContain(archiveId.ToString());
        }

        // Importing it updates the game and keeps the archive it already had
        var result = await GetService<IPackageImporter>().ImportStreamAsync(new MemoryStream(withoutArchive), manifestType: ManifestType.Game);

        result.RecordId.ShouldBe(gameId);
        (await GetService<ArchiveService>().ExistsAsync(archiveId)).ShouldBeTrue();
    }

    [Fact]
    public async Task APackageIsWrittenToAStreamThatCantSeek()
    {
        var (gameId, _, _) = await ImportGameAsync();

        await using var output = new ForwardOnlyStream();

        await GetService<IPackageExporter>().ExportAsync(ImportExportRecordType.Game, gameId, output);

        using var zip = new ZipArchive(new MemoryStream(output.Written.ToArray()));

        ManifestOf(zip).ShouldContain(gameId.ToString());
    }

    [Fact]
    public async Task AMissingRecordIsReportedAsSuch()
    {
        var exporter = GetService<IPackageExporter>();

        await Should.ThrowAsync<KeyNotFoundException>(() => exporter.DescribeAsync(ImportExportRecordType.Game, Guid.NewGuid()));
        await Should.ThrowAsync<KeyNotFoundException>(() => exporter.ExportAsync(ImportExportRecordType.Tool, Guid.NewGuid(), Stream.Null));
        await Should.ThrowAsync<ArgumentException>(() => exporter.DescribeAsync(ImportExportRecordType.Archive, Guid.NewGuid()));
    }

    static async Task<byte[]> ExportAsync(IPackageExporter exporter, Guid gameId, params Guid[] excluded)
    {
        using var output = new MemoryStream();

        await exporter.ExportAsync(ImportExportRecordType.Game, gameId, output, excluded);

        return output.ToArray();
    }

    static string ManifestOf(ZipArchive zip)
    {
        using var reader = new StreamReader(zip.GetEntry(ManifestHelper.ManifestFilename)!.Open());

        return reader.ReadToEnd();
    }

    /// <summary>Imports a game with one archive through the server's importer, as a sync would.</summary>
    async Task<(Guid GameId, Guid ArchiveId, byte[] ArchiveBytes)> ImportGameAsync()
    {
        await EnsureStorageLocationsExistAsync();

        var gameId = Guid.NewGuid();
        var archiveId = Guid.NewGuid();
        var archiveBytes = ZipOf("game.exe", $"archive {archiveId}");

        await using var package = new MemoryStream();

        using (var zip = new ZipArchive(package, ZipArchiveMode.Create, leaveOpen: true))
        {
            await using (var writer = new StreamWriter(zip.CreateEntry(ManifestHelper.ManifestFilename).Open(), Encoding.UTF8))
            {
                await writer.WriteAsync(ManifestHelper.Serialize(new SDK.Models.Manifest.Game
                {
                    Id = gameId,
                    // Without one, the manifest is read as a legacy export, which names archive entries differently
                    ManifestVersion = VersionHelper.GetCurrentVersion().ToString(),
                    // Unique, since the importer also matches games by title
                    Title = $"Exported By A Plugin {gameId:N}",
                    Version = "1.0",
                    DirectoryName = $"ExportedByAPlugin{gameId:N}",
                    Archives =
                    [
                        new SDK.Models.Manifest.Archive
                        {
                            Id = archiveId,
                            Version = "1.0",
                            ObjectKey = Guid.NewGuid().ToString(),
                            CompressedSize = archiveBytes.Length,
                        },
                    ],
                }));
            }

            await using var entry = zip.CreateEntry($"Archives/{archiveId}").Open();

            await entry.WriteAsync(archiveBytes);
        }

        package.Position = 0;

        var result = await GetService<IPackageImporter>().ImportStreamAsync(package, manifestType: ManifestType.Game);

        result.RecordId.ShouldBe(gameId);

        return (gameId, archiveId, archiveBytes);
    }

    /// <summary>A game's archive is itself a zip of the game's files.</summary>
    static byte[] ZipOf(string name, string content)
    {
        using var buffer = new MemoryStream();

        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        using (var writer = new StreamWriter(zip.CreateEntry(name).Open()))
            writer.Write(content);

        return buffer.ToArray();
    }

    /// <summary>Like an HTTP response body: written forwards only, with no length or position.</summary>
    sealed class ForwardOnlyStream : Stream
    {
        public MemoryStream Written { get; } = new();

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => Written.Write(buffer, offset, count);
    }
}
