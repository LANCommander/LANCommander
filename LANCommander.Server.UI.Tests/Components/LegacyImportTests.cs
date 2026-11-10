using System.IO.Compression;
using System.Text;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using LANCommander.Server.ImportExport.Services;
using LANCommander.Server.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>
/// Packages from servers before manifests carried a ManifestVersion use another layout: genres,
/// keys and the engine as plain strings, one entry per multiplayer type, and archives and media
/// stored under their object key and file id rather than their id.
/// </summary>
[Collection("BUnit")]
public class LegacyImportTests
{
    private readonly BUnitServerFixture _fixture;

    public LegacyImportTests(BUnitServerFixture fixture) => _fixture = fixture;

    internal sealed record LegacyPackage(string Path, Guid GameId, Guid ScriptId, Guid MediaId, Guid ArchiveId, string Title, string Genre, string Key, byte[] MediaBytes);

    internal static async Task<LegacyPackage> WriteLegacyPackageAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var gameId = Guid.NewGuid();
        var scriptId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();
        var mediaFileId = Guid.NewGuid();
        var archiveId = Guid.NewGuid();
        var objectKey = Guid.NewGuid();
        var title = $"Legacy Game {suffix}";
        var genre = $"Legacy Genre {suffix}";
        var key = $"LEGACY-{suffix}";

        var manifest = $"""
            Id: {gameId}
            Title: '{title}'
            SortTitle: '{title}'
            Description: Imported from an old server
            Notes:
            Type: MainGame
            ReleasedOn: 2006-11-28T00:00:00.0000000
            Engine: Legacy Engine {suffix}
            Genre:
            - {genre}
            Tags:
            - multiplayer
            Publishers:
            - Old Publisher {suffix}
            Developers:
            - Old Developer {suffix}
            Collections:
            Version: v2.02
            Actions:
            - Name: Play
              Arguments: -windowed
              Path: game.exe
              WorkingDirectory: '{"{InstallDir}"}'
              IsPrimaryAction: true
              SortOrder: 0
            - Name: Editor
              Arguments:
              Path: editor.exe
              WorkingDirectory: '{"{InstallDir}"}'
              IsPrimaryAction: false
              SortOrder: 1
            Singleplayer: true
            LocalMultiplayer:
            LanMultiplayer:
              MinPlayers: 2
              MaxPlayers: 8
              Description: Skirmish
              NetworkProtocol: TCPIP
            OnlineMultiplayer:
            SavePaths:
            - Type: File
              Path: options.ini
              WorkingDirectory: '%APPDATA%\Legacy'
              IsRegex: false
              Entries:
              Id: {Guid.NewGuid()}
            Keys:
            - {key}
            Scripts:
            - Type: Install
              Name: Install
              Description: Writes the registry
              RequiresAdmin: true
              Contents:
              Id: {scriptId}
            Media:
            - FileId: {mediaFileId}
              Name: Front
              Type: Cover
              SourceUrl: ''
              MimeType: image/png
              Crc32:
              Id: {mediaId}
            Archives:
            - Id: {archiveId}
              ObjectKey: {objectKey}
              Version: v2.02
              Changelog: Final patch
            DependentGames:
            Redistributables:
            CustomFields:
            """;

        var mediaBytes = Guid.NewGuid().ToByteArray();
        var path = System.IO.Path.Combine(ImportPackages.TempDirectory(), "legacy.lcx");

        await using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            await using (var writer = new StreamWriter(zip.CreateEntry(ManifestHelper.ManifestFilename).Open(), Encoding.UTF8))
                await writer.WriteAsync(manifest);

            await using (var writer = new StreamWriter(zip.CreateEntry($"Scripts/{scriptId}").Open()))
                await writer.WriteAsync("Write-Host 'legacy install'");

            await using (var entry = zip.CreateEntry($"Media/{mediaFileId}").Open())
                await entry.WriteAsync(mediaBytes);

            await using (var entry = zip.CreateEntry($"Archives/{objectKey}").Open())
                await entry.WriteAsync(ImportPackages.ZipBytes("legacy files"));
        }

        return new LegacyPackage(path, gameId, scriptId, mediaId, archiveId, title, genre, key, mediaBytes);
    }

    [Fact]
    public async Task LegacyPackageImportsEverythingItCarries()
    {
        using var scope = _fixture.Factory.RealServices.CreateScope();
        var services = scope.ServiceProvider;

        var package = await WriteLegacyPackageAsync();

        var result = await services.GetRequiredService<ImportRunner>().RunFileAsync(package.Path);

        Assert.Empty(result.Errors);
        Assert.Equal(ManifestType.Game, result.ManifestType);

        var game = await services.GetRequiredService<GameService>().Include(g => g.Genres, g => g.Engine!).GetAsync(package.GameId);

        Assert.NotNull(game);
        Assert.Equal(package.Title, game.Title);
        Assert.Contains(game.Genres!, g => g.Name == package.Genre);
        Assert.NotNull(game.Engine);

        var actions = await services.GetRequiredService<ActionService>().GetAsync(a => a.GameId == package.GameId);
        Assert.Equal(["Editor|Secondary", "Play|Primary"], actions.Select(a => $"{a.Name}|{a.Type}").Order());

        var modes = await services.GetRequiredService<MultiplayerModeService>().GetAsync(m => m.GameId == package.GameId);
        Assert.Equal(["LAN|Skirmish|2-8"], modes.Select(m => $"{m.Type}|{m.Description}|{m.MinPlayers}-{m.MaxPlayers}"));

        Assert.Equal([package.Key], (await services.GetRequiredService<KeyService>().GetAsync(k => k.GameId == package.GameId)).Select(k => k.Value));
        Assert.Single(await services.GetRequiredService<SavePathService>().GetAsync(p => p.GameId == package.GameId));

        var script = await services.GetRequiredService<ScriptService>().GetAsync(package.ScriptId);
        Assert.Equal("Write-Host 'legacy install'", script?.Contents);

        var mediaService = services.GetRequiredService<MediaService>();
        var media = await mediaService.Include(m => m.StorageLocation!).GetAsync(package.MediaId);
        Assert.NotNull(media);
        Assert.Equal(package.MediaBytes, await File.ReadAllBytesAsync(MediaService.GetMediaPath(media)));

        var archiveService = services.GetRequiredService<ArchiveService>();
        var archive = await archiveService.GetAsync(package.ArchiveId);
        Assert.NotNull(archive);
        Assert.True(File.Exists(await archiveService.GetArchiveFileLocationAsync(archive)), "The archive's file was not written");
    }

    /// <summary>
    /// A current-format package that just lacks a ManifestVersion (e.g. written by hand or by a tool)
    /// must not be read as a legacy one, which has no multiplayer mode list or saves.
    /// </summary>
    [Fact]
    public async Task CurrentPackageWithoutAManifestVersionKeepsItsModesAndSaves()
    {
        using var scope = _fixture.Factory.RealServices.CreateScope();
        var services = scope.ServiceProvider;

        var manifest = ImportPackages.Game();
        manifest.ManifestVersion = null;

        var result = await services.GetRequiredService<ImportRunner>().RunFileAsync(await ImportPackages.WriteAsync(manifest));

        Assert.Empty(result.Errors);
        Assert.Equal(2, (await services.GetRequiredService<MultiplayerModeService>().GetAsync(m => m.GameId == manifest.Id)).Count);
        Assert.Single(await services.GetRequiredService<GameSaveService>().GetAsync(s => s.GameId == manifest.Id));
    }
}
