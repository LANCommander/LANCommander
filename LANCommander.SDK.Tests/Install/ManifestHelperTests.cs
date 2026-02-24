using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Services;
using ManifestGame = LANCommander.SDK.Models.Manifest.Game;
using ManifestAction = LANCommander.SDK.Models.Manifest.Action;
using ManifestRedistributable = LANCommander.SDK.Models.Manifest.Redistributable;
using ManifestGenre = LANCommander.SDK.Models.Manifest.Genre;
using ManifestSavePath = LANCommander.SDK.Models.Manifest.SavePath;

namespace LANCommander.SDK.Tests.Install;

public class ManifestHelperTests : IDisposable
{
    private readonly string _tempDir;

    public ManifestHelperTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"lc-manifest-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private ManifestGame MakeManifest(Guid? id = null, string title = "Test Game", GameType type = GameType.MainGame)
    {
        return new ManifestGame
        {
            Id = id ?? Guid.NewGuid(),
            Title = title,
            Type = type,
            Version = "1.0.0"
        };
    }

    // ── GetPath ───────────────────────────────────────────────────────────────

    [Fact]
    public void GetPath_ReturnsPathInsideMetadataDirectory()
    {
        var gameId = Guid.NewGuid();
        var expected = GameClient.GetMetadataFilePath(_tempDir, gameId, ManifestHelper.ManifestFilename);

        var result = ManifestHelper.GetPath(_tempDir, gameId);

        Assert.Equal(expected, result);
    }

    // ── Exists ────────────────────────────────────────────────────────────────

    [Fact]
    public void Exists_WhenManifestFileIsAbsent_ReturnsFalse()
    {
        var result = ManifestHelper.Exists(_tempDir, Guid.NewGuid());

        Assert.False(result);
    }

    [Fact]
    public void Exists_WhenManifestFileIsPresent_ReturnsTrue()
    {
        var manifest = MakeManifest();
        ManifestHelper.Write(manifest, _tempDir);

        Assert.True(ManifestHelper.Exists(_tempDir, manifest.Id));
    }

    // ── Write / Read round-trip ───────────────────────────────────────────────

    [Fact]
    public void Write_CreatesManifestFile()
    {
        var manifest = MakeManifest();

        ManifestHelper.Write(manifest, _tempDir);

        var path = ManifestHelper.GetPath(_tempDir, manifest.Id);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Read_ReturnsNullWhenManifestDoesNotExist()
    {
        var result = ManifestHelper.Read<ManifestGame>(_tempDir, Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public void WriteAndRead_RoundTripsId()
    {
        var manifest = MakeManifest();
        ManifestHelper.Write(manifest, _tempDir);

        var loaded = ManifestHelper.Read<ManifestGame>(_tempDir, manifest.Id);

        Assert.NotNull(loaded);
        Assert.Equal(manifest.Id, loaded.Id);
    }

    [Fact]
    public void WriteAndRead_RoundTripsTitle()
    {
        var manifest = MakeManifest(title: "Half-Life 2");
        ManifestHelper.Write(manifest, _tempDir);

        var loaded = ManifestHelper.Read<ManifestGame>(_tempDir, manifest.Id);

        Assert.Equal("Half-Life 2", loaded.Title);
    }

    [Fact]
    public void WriteAndRead_RoundTripsGameType()
    {
        var manifest = MakeManifest(type: GameType.Expansion);
        ManifestHelper.Write(manifest, _tempDir);

        var loaded = ManifestHelper.Read<ManifestGame>(_tempDir, manifest.Id);

        Assert.Equal(GameType.Expansion, loaded.Type);
    }

    [Fact]
    public void WriteAndRead_RoundTripsVersion()
    {
        var manifest = MakeManifest();
        manifest.Version = "2.3.4";
        ManifestHelper.Write(manifest, _tempDir);

        var loaded = ManifestHelper.Read<ManifestGame>(_tempDir, manifest.Id);

        Assert.Equal("2.3.4", loaded.Version);
    }

    [Fact]
    public void WriteAndRead_RoundTripsActions()
    {
        var manifest = MakeManifest();
        manifest.Actions.Add(new ManifestAction
        {
            Name = "Play",
            Path = "game.exe",
            IsPrimaryAction = true,
            SortOrder = 0
        });
        ManifestHelper.Write(manifest, _tempDir);

        var loaded = ManifestHelper.Read<ManifestGame>(_tempDir, manifest.Id);

        Assert.Single(loaded.Actions);
        Assert.Equal("Play", loaded.Actions.First().Name);
        Assert.Equal("game.exe", loaded.Actions.First().Path);
        Assert.True(loaded.Actions.First().IsPrimaryAction);
    }

    [Fact]
    public void WriteAndRead_RoundTripsAddons()
    {
        var manifest = MakeManifest();
        var addon = MakeManifest(title: "Expansion Pack", type: GameType.Expansion);
        manifest.Addons.Add(addon);
        ManifestHelper.Write(manifest, _tempDir);

        var loaded = ManifestHelper.Read<ManifestGame>(_tempDir, manifest.Id);

        Assert.Single(loaded.Addons);
        Assert.Equal(addon.Id, loaded.Addons.First().Id);
        Assert.Equal("Expansion Pack", loaded.Addons.First().Title);
        Assert.Equal(GameType.Expansion, loaded.Addons.First().Type);
    }

    // ── Async variants ────────────────────────────────────────────────────────

    [Fact]
    public async Task ReadAsync_ReturnsNullWhenManifestDoesNotExist()
    {
        var result = await ManifestHelper.ReadAsync<ManifestGame>(_tempDir, Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task WriteAsyncAndReadAsync_RoundTrips()
    {
        var manifest = MakeManifest(title: "Async Test Game");

        await ManifestHelper.WriteAsync(manifest, _tempDir);
        var loaded = await ManifestHelper.ReadAsync<ManifestGame>(_tempDir, manifest.Id);

        Assert.NotNull(loaded);
        Assert.Equal(manifest.Id, loaded.Id);
        Assert.Equal("Async Test Game", loaded.Title);
    }

    // ── Serialize / Deserialize ───────────────────────────────────────────────

    [Fact]
    public void SerializeAndDeserialize_RoundTripsManifest()
    {
        var manifest = MakeManifest(title: "Serialization Test");
        manifest.Addons.Add(MakeManifest(title: "Addon", type: GameType.Mod));

        var yaml = ManifestHelper.Serialize(manifest);
        var deserialized = ManifestHelper.Deserialize<ManifestGame>(yaml);

        Assert.Equal(manifest.Id, deserialized.Id);
        Assert.Equal("Serialization Test", deserialized.Title);
        Assert.Single(deserialized.Addons);
        Assert.Equal(GameType.Mod, deserialized.Addons.First().Type);
    }

    [Fact]
    public void TryDeserialize_WithValidYaml_ReturnsTrueAndManifest()
    {
        var manifest = MakeManifest(title: "Valid YAML");
        var yaml = ManifestHelper.Serialize(manifest);

        var success = ManifestHelper.TryDeserialize<ManifestGame>(yaml, out var result);

        Assert.True(success);
        Assert.NotNull(result);
        Assert.Equal(manifest.Id, result.Id);
    }

    [Fact]
    public void TryDeserialize_WithInvalidYaml_ReturnsFalse()
    {
        var success = ManifestHelper.TryDeserialize<ManifestGame>("{ invalid yaml [[[", out var result);

        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void TryDeserialize_WithEmptyString_ReturnsTrueWithNullManifest()
    {
        // YamlDotNet treats an empty document as a null value — it doesn't throw,
        // so TryDeserialize returns true with a null out-parameter.
        var success = ManifestHelper.TryDeserialize<ManifestGame>("", out var result);

        Assert.True(success);
        Assert.Null(result);
    }

    // ── Serialize format ──────────────────────────────────────────────────────

    [Fact]
    public void Serialize_ProducesPascalCaseYaml()
    {
        var manifest = MakeManifest(title: "Format Check");

        var yaml = ManifestHelper.Serialize(manifest);

        // PascalCase naming convention means field names match the C# property names
        Assert.Contains("Id:", yaml);
        Assert.Contains("Title:", yaml);
        Assert.Contains("Version:", yaml);
    }

    [Fact]
    public void Serialize_IsDeterministic()
    {
        var manifest = MakeManifest(title: "Stable Output");

        var yaml1 = ManifestHelper.Serialize(manifest);
        var yaml2 = ManifestHelper.Serialize(manifest);

        Assert.Equal(yaml1, yaml2);
    }

    // ── Deserialize IgnoreUnmatchedProperties ─────────────────────────────────

    [Fact]
    public void Deserialize_WithUnknownProperties_DoesNotThrow()
    {
        var yaml = """
            Id: 00000000-0000-0000-0000-000000000001
            Title: Known Title
            UnknownField: some value
            AnotherUnknownField: 42
            """;

        var ex = Record.Exception(() => ManifestHelper.Deserialize<ManifestGame>(yaml));

        Assert.Null(ex);
    }

    [Fact]
    public void Deserialize_WithUnknownProperties_StillPopulatesKnownFields()
    {
        var yaml = """
            Id: 00000000-0000-0000-0000-000000000002
            Title: Known Title
            UnknownField: ignored
            """;

        var result = ManifestHelper.Deserialize<ManifestGame>(yaml);

        Assert.Equal(new Guid("00000000-0000-0000-0000-000000000002"), result.Id);
        Assert.Equal("Known Title", result.Title);
    }

    // ── Read<T> without Guid (root Manifest.yml) ─────────────────────────────

    [Fact]
    public void Read_WithoutId_ReadsManifestFromRootOfInstallDirectory()
    {
        var manifest = MakeManifest(title: "Root Manifest");
        var rootPath = Path.Combine(_tempDir, ManifestHelper.ManifestFilename);
        File.WriteAllText(rootPath, ManifestHelper.Serialize(manifest));

        var loaded = ManifestHelper.Read<ManifestGame>(_tempDir);

        Assert.NotNull(loaded);
        Assert.Equal(manifest.Id, loaded.Id);
        Assert.Equal("Root Manifest", loaded.Title);
    }

    [Fact]
    public void Read_WithoutId_WhenFileNotFound_ThrowsFileNotFoundException()
    {
        // Unlike Read(installDir, Guid) which returns null, the no-Guid overload
        // calls File.ReadAllText directly and throws if the file is absent.
        Assert.Throws<FileNotFoundException>(() =>
            ManifestHelper.Read<ManifestGame>(_tempDir));
    }

    // ── ReadAsync<T> without Guid (root Manifest.yml) ─────────────────────────

    [Fact]
    public async Task ReadAsync_WithoutId_ReadsManifestFromRootOfInstallDirectory()
    {
        var manifest = MakeManifest(title: "Async Root Manifest");
        var rootPath = Path.Combine(_tempDir, ManifestHelper.ManifestFilename);
        await File.WriteAllTextAsync(rootPath, ManifestHelper.Serialize(manifest));

        var loaded = await ManifestHelper.ReadAsync<ManifestGame>(_tempDir);

        Assert.NotNull(loaded);
        Assert.Equal(manifest.Id, loaded.Id);
        Assert.Equal("Async Root Manifest", loaded.Title);
    }

    [Fact]
    public async Task ReadAsync_WithoutId_WhenFileNotFound_ThrowsFileNotFoundException()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            ManifestHelper.ReadAsync<ManifestGame>(_tempDir));
    }

    // ── Write / WriteAsync return values ─────────────────────────────────────

    [Fact]
    public void Write_ReturnsPathToWrittenFile()
    {
        var manifest = MakeManifest();

        var returnedPath = ManifestHelper.Write(manifest, _tempDir);

        var expectedPath = ManifestHelper.GetPath(_tempDir, manifest.Id);
        Assert.Equal(expectedPath, returnedPath);
        Assert.True(File.Exists(returnedPath));
    }

    [Fact]
    public async Task WriteAsync_ReturnsPathToWrittenFile()
    {
        var manifest = MakeManifest();

        var returnedPath = await ManifestHelper.WriteAsync(manifest, _tempDir);

        var expectedPath = ManifestHelper.GetPath(_tempDir, manifest.Id);
        Assert.Equal(expectedPath, returnedPath);
        Assert.True(File.Exists(returnedPath));
    }

    // ── Write overwrites ──────────────────────────────────────────────────────

    [Fact]
    public void Write_WhenCalledTwice_OverwritesExistingManifest()
    {
        var id = Guid.NewGuid();
        var first = MakeManifest(id: id, title: "First Title");
        var second = MakeManifest(id: id, title: "Second Title");

        ManifestHelper.Write(first, _tempDir);
        ManifestHelper.Write(second, _tempDir);

        var loaded = ManifestHelper.Read<ManifestGame>(_tempDir, id);
        Assert.Equal("Second Title", loaded.Title);
    }

    // ── Exists after WriteAsync ───────────────────────────────────────────────

    [Fact]
    public async Task Exists_AfterWriteAsync_ReturnsTrue()
    {
        var manifest = MakeManifest();
        await ManifestHelper.WriteAsync(manifest, _tempDir);

        Assert.True(ManifestHelper.Exists(_tempDir, manifest.Id));
    }

    // ── GetPath structure ─────────────────────────────────────────────────────

    [Fact]
    public void GetPath_PathContainsLanCommanderSubdirectory()
    {
        var result = ManifestHelper.GetPath(_tempDir, Guid.NewGuid());

        Assert.Contains(".lancommander", result);
    }

    [Fact]
    public void GetPath_PathContainsGameId()
    {
        var id = Guid.NewGuid();

        var result = ManifestHelper.GetPath(_tempDir, id);

        Assert.Contains(id.ToString(), result);
    }

    [Fact]
    public void GetPath_PathEndsWithManifestFilename()
    {
        var result = ManifestHelper.GetPath(_tempDir, Guid.NewGuid());

        Assert.Equal(ManifestHelper.ManifestFilename, Path.GetFileName(result));
    }

    // ── Write/Read with non-Game IKeyedModel ──────────────────────────────────

    [Fact]
    public void WriteAndRead_WithRedistributableManifest_RoundTrips()
    {
        var redist = new ManifestRedistributable
        {
            Id = Guid.NewGuid(),
            Name = "DirectX Runtime",
            Description = "Required runtime libraries"
        };

        ManifestHelper.Write(redist, _tempDir);
        var loaded = ManifestHelper.Read<ManifestRedistributable>(_tempDir, redist.Id);

        Assert.NotNull(loaded);
        Assert.Equal(redist.Id, loaded.Id);
        Assert.Equal("DirectX Runtime", loaded.Name);
        Assert.Equal("Required runtime libraries", loaded.Description);
    }

    // ── Round-trips for richer collection fields ──────────────────────────────

    [Fact]
    public void WriteAndRead_RoundTripsGenres()
    {
        var manifest = MakeManifest();
        manifest.Genres.Add(new ManifestGenre { Name = "Action" });
        manifest.Genres.Add(new ManifestGenre { Name = "RPG" });
        ManifestHelper.Write(manifest, _tempDir);

        var loaded = ManifestHelper.Read<ManifestGame>(_tempDir, manifest.Id);

        Assert.Equal(2, loaded.Genres.Count);
        Assert.Contains(loaded.Genres, g => g.Name == "Action");
        Assert.Contains(loaded.Genres, g => g.Name == "RPG");
    }

    [Fact]
    public void WriteAndRead_RoundTripsSavePaths()
    {
        var manifest = MakeManifest();
        manifest.SavePaths.Add(new ManifestSavePath
        {
            Id = Guid.NewGuid(),
            Path = "%APPDATA%\\MyGame\\Saves",
            Type = SavePathType.File,
            IsRegex = false
        });
        ManifestHelper.Write(manifest, _tempDir);

        var loaded = ManifestHelper.Read<ManifestGame>(_tempDir, manifest.Id);

        Assert.Single(loaded.SavePaths);
        Assert.Equal("%APPDATA%\\MyGame\\Saves", loaded.SavePaths.First().Path);
        Assert.Equal(SavePathType.File, loaded.SavePaths.First().Type);
        Assert.False(loaded.SavePaths.First().IsRegex);
    }
}
