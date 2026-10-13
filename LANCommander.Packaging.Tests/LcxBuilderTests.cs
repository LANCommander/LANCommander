using System.IO.Compression;
using LANCommander.Packaging.Changes;
using LANCommander.Packaging.LCX;
using LANCommander.Packaging.Models;
using LANCommander.SDK.Helpers;
using Shouldly;

namespace LANCommander.Packaging.Tests;

public class LcxBuilderTests : IDisposable
{
    private readonly string _workingDirectory =
        Path.Combine(Path.GetTempPath(), "lcx-tests-" + Guid.NewGuid().ToString("N"));

    public LcxBuilderTests() => Directory.CreateDirectory(_workingDirectory);

    [Fact]
    public async Task ProducesAManifestAndAnArchive()
    {
        var package = BuildPackage(("game.exe", "binary"), (@"Data\config.ini", "settings"));

        await LCXBuilder.BuildAsync(package);

        using var archive = ZipFile.OpenRead(package.OutputPath);

        archive.GetEntry(ManifestHelper.ManifestFilename).ShouldNotBeNull();
        archive.Entries.Count(e => e.FullName.StartsWith("Archives/")).ShouldBe(1);
    }

    [Fact]
    public async Task ArchiveEntriesArePathsRelativeToTheInstallDirectory()
    {
        var package = BuildPackage(("game.exe", "binary"), (@"Data\config.ini", "settings"));

        await LCXBuilder.BuildAsync(package);

        using var archive = ZipFile.OpenRead(package.OutputPath);

        var innerEntry = archive.Entries.First(e => e.FullName.StartsWith("Archives/"));

        await using var innerStream = innerEntry.Open();
        await using var buffer = new MemoryStream();

        await innerStream.CopyToAsync(buffer);
        buffer.Position = 0;

        using var innerArchive = new ZipArchive(buffer, ZipArchiveMode.Read);

        var names = innerArchive.Entries.Select(e => e.FullName).ToList();

        names.ShouldContain("game.exe");
        names.ShouldContain("Data/config.ini");
        names.ShouldNotContain(n => n.Contains(':'));
    }

    [Fact]
    public async Task ArchiveEntriesUseForwardSlashes()
    {
        var package = BuildPackage(
            ("game.exe", "binary"),
            (@"Data\config.ini", "settings"),
            (@"Data\Sounds\music.ogg", "audio"));

        await LCXBuilder.BuildAsync(package);

        var names = await ReadArchiveEntryNamesAsync(package.OutputPath);

        names.ShouldNotContain(n => n.Contains('\\'));
        names.ShouldContain("Data/config.ini");
        names.ShouldContain("Data/Sounds/music.ogg");
    }

    [Fact]
    public async Task RecordsPlausibleArchiveSizes()
    {
        // The old builder measured the outer stream position, which included zip headers and
        // would have been outright wrong for a second archive.
        var package = BuildPackage(("game.exe", new string('a', 4096)));

        await LCXBuilder.BuildAsync(package);

        var manifest = await ReadManifestAsync(package.OutputPath);

        var archive = manifest.Archives.ShouldHaveSingleItem();

        archive.UncompressedSize.ShouldBe(4096);
        archive.CompressedSize.ShouldBeGreaterThan(0);
        archive.CompressedSize.ShouldBeLessThan(archive.UncompressedSize);
    }

    [Fact]
    public async Task WritesGeneratedScriptsAndRecordsThemInTheManifest()
    {
        var package = BuildPackage(("game.exe", "binary"));

        package.SelectedRegistryEntries.Add(new RegistryChange
        {
            Verb = "REG WRITE",
            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Example",
            ValueName = "InstallPath",
        });

        await LCXBuilder.BuildAsync(package);

        using var archive = ZipFile.OpenRead(package.OutputPath);

        var scriptEntries = archive.Entries.Where(e => e.FullName.StartsWith("Scripts/")).ToList();

        scriptEntries.Count.ShouldBe(2);

        var manifest = await ReadManifestAsync(package.OutputPath);

        manifest.Scripts.Count.ShouldBe(2);

        // Every script record must have a blob to go with it, addressed by record id.
        foreach (var script in manifest.Scripts)
            scriptEntries.ShouldContain(e => e.FullName == $"Scripts/{script.Id}");
    }

    [Fact]
    public async Task StampsManifestProvenance()
    {
        var package = BuildPackage(("game.exe", "binary"));

        await LCXBuilder.BuildAsync(package);

        var manifest = await ReadManifestAsync(package.OutputPath);

        manifest.Id.ShouldNotBe(Guid.Empty);
        manifest.ManifestVersion.ShouldBe(LCXBuilder.ManifestVersion);
        manifest.CreatedBy.ShouldBe(LCXBuilder.CreatedBy);
        manifest.IsLegacyManifest().ShouldBeFalse();
    }

    [Fact]
    public async Task SkipsSelectedFilesThatNoLongerExist()
    {
        var package = BuildPackage(("game.exe", "binary"));

        package.SelectedFiles.Add(Path.Combine(package.InstallDirectory, "deleted.dat"));

        await Should.NotThrowAsync(() => LCXBuilder.BuildAsync(package));
    }

    [Fact]
    public async Task ThrowsWhenNoOutputPathWasSet()
    {
        var package = BuildPackage(("game.exe", "binary"));

        package.OutputPath = string.Empty;

        await Should.ThrowAsync<InvalidOperationException>(() => LCXBuilder.BuildAsync(package));
    }

    [Fact]
    public async Task PrebuiltArchiveOverloadPreservesCallerAssignedIdsAndProvenance()
    {
        var gameId = Guid.NewGuid();
        var archiveId = Guid.NewGuid();
        var outputPath = Path.Combine(_workingDirectory, "streamed.lcx");
        var manifest = new SDK.Models.Manifest.Game
        {
            Id = gameId,
            Title = "Streamed Example",
            Version = "2.0",
        };

        await using var innerArchive = await BuildInnerArchiveAsync(("game.exe", "binary"));

        await LCXBuilder.BuildFromArchiveAsync(
            outputPath,
            manifest,
            new SDK.Models.Manifest.Archive
            {
                Id = archiveId,
                Version = "2.0",
                UncompressedSize = 6,
            },
            innerArchive,
            createdBy: "Recomp Catalog Tests");

        var written = await ReadManifestAsync(outputPath);

        written.Id.ShouldBe(gameId);
        written.CreatedBy.ShouldBe("Recomp Catalog Tests");
        written.UpdatedBy.ShouldBe("Recomp Catalog Tests");
        written.Archives.ShouldHaveSingleItem().Id.ShouldBe(archiveId);
        written.Archives.Single().ObjectKey.ShouldBe(archiveId.ToString());
        written.Archives.Single().CompressedSize.ShouldBe(innerArchive.Length);
    }

    [Fact]
    public async Task PrebuiltArchiveOverloadRejectsUnassignedArchiveIds()
    {
        await using var innerArchive = await BuildInnerArchiveAsync(("game.exe", "binary"));

        await Should.ThrowAsync<InvalidOperationException>(() =>
            LCXBuilder.BuildFromArchiveAsync(
                new MemoryStream(),
                new SDK.Models.Manifest.Game { Title = "Example" },
                new SDK.Models.Manifest.Archive { Version = "1.0" },
                innerArchive,
                createdBy: "Tests"));
    }

    [Fact]
    public async Task PrebuiltArchiveOverloadDoesNotReplaceExistingFileWhenValidationFails()
    {
        var outputPath = Path.Combine(_workingDirectory, "existing.lcx");
        await File.WriteAllTextAsync(outputPath, "existing package");
        await using var innerArchive = await BuildInnerArchiveAsync(("game.exe", "binary"));

        await Should.ThrowAsync<InvalidOperationException>(() =>
            LCXBuilder.BuildFromArchiveAsync(
                outputPath,
                new SDK.Models.Manifest.Game { Title = "Example" },
                new SDK.Models.Manifest.Archive { Version = "1.0" },
                innerArchive,
                createdBy: "Tests"));

        (await File.ReadAllTextAsync(outputPath)).ShouldBe("existing package");
        Directory.GetFiles(_workingDirectory, "*.tmp").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("archive")]
    [InlineData("script")]
    [InlineData("media")]
    [InlineData("save")]
    public async Task PrebuiltArchiveOverloadRejectsManifestEntriesWithoutContent(string entryType)
    {
        var manifest = new SDK.Models.Manifest.Game { Title = "Example" };

        switch (entryType)
        {
            case "archive":
                manifest.Archives.Add(new SDK.Models.Manifest.Archive { Id = Guid.NewGuid() });
                break;
            case "script":
                manifest.Scripts.Add(new SDK.Models.Manifest.Script { Id = Guid.NewGuid() });
                break;
            case "media":
                manifest.Media.Add(new SDK.Models.Manifest.Media { Id = Guid.NewGuid() });
                break;
            case "save":
                manifest.Saves.Add(new SDK.Models.Manifest.Save { Id = Guid.NewGuid() });
                break;
        }

        await using var innerArchive = await BuildInnerArchiveAsync(("game.exe", "binary"));

        await Should.ThrowAsync<ArgumentException>(() =>
            LCXBuilder.BuildFromArchiveAsync(
                new MemoryStream(),
                manifest,
                new SDK.Models.Manifest.Archive { Id = Guid.NewGuid(), Version = "1.0" },
                innerArchive,
                createdBy: "Tests"));
    }

    private static async Task<SDK.Models.Manifest.Game> ReadManifestAsync(string lcxPath)
    {
        using var archive = ZipFile.OpenRead(lcxPath);

        var entry = archive.GetEntry(ManifestHelper.ManifestFilename).ShouldNotBeNull();

        await using var stream = entry.Open();
        using var reader = new StreamReader(stream);

        return ManifestHelper.Deserialize<SDK.Models.Manifest.Game>(await reader.ReadToEndAsync());
    }

    private static async Task<List<string>> ReadArchiveEntryNamesAsync(string packagePath)
    {
        using var archive = ZipFile.OpenRead(packagePath);

        var innerEntry = archive.Entries.First(e => e.FullName.StartsWith("Archives/"));

        await using var innerStream = innerEntry.Open();
        await using var buffer = new MemoryStream();

        await innerStream.CopyToAsync(buffer);
        buffer.Position = 0;

        using var innerArchive = new ZipArchive(buffer, ZipArchiveMode.Read);

        return [.. innerArchive.Entries.Select(e => e.FullName)];
    }

    private static async Task<MemoryStream> BuildInnerArchiveAsync(
        params (string RelativePath, string Contents)[] files)
    {
        var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (relativePath, contents) in files)
            {
                var entry = archive.CreateEntry(relativePath);
                await using var entryStream = entry.Open();
                await using var writer = new StreamWriter(entryStream);
                await writer.WriteAsync(contents);
            }
        }

        stream.Position = 0;
        return stream;
    }

    private PackageDefinition BuildPackage(params (string RelativePath, string Contents)[] files)
    {
        var installDirectory = Path.Combine(_workingDirectory, "install");

        Directory.CreateDirectory(installDirectory);

        var package = new PackageDefinition
        {
            InstallDirectory = installDirectory,
            OutputPath = Path.Combine(_workingDirectory, "package.lcx"),
            Manifest = new SDK.Models.Manifest.Game { Title = "Example", Version = "1.0" },
        };

        foreach (var (relativePath, contents) in files)
        {
            var fullPath = Path.Combine(installDirectory, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, contents);

            package.SelectedFiles.Add(fullPath);
        }

        return package;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_workingDirectory))
                Directory.Delete(_workingDirectory, recursive: true);
        }
        catch
        {
            // Best effort cleanup of a temp directory.
        }
    }
}
