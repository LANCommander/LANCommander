using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Services;
using ManifestGame = LANCommander.SDK.Models.Manifest.Game;
using ManifestAction = LANCommander.SDK.Models.Manifest.Action;

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
}
