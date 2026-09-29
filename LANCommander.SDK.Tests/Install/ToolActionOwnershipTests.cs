using LANCommander.SDK.Helpers;
using LANCommander.SDK.Services;
using ManifestAction = LANCommander.SDK.Models.Manifest.Action;
using ManifestGame = LANCommander.SDK.Models.Manifest.Game;
using ManifestTool = LANCommander.SDK.Models.Manifest.Tool;

namespace LANCommander.SDK.Tests.Install;

/// <summary>
/// A tool's actions are merged into the game's Play menu, but the manifest Action they travel as
/// carried no owner, so picking one ran the game's Before Start / After Stop scripts instead of the
/// tool's. Ownership now rides along on Action.ToolId.
/// </summary>
public class ToolActionOwnershipTests : IDisposable
{
    private readonly string _tempDir;

    public ToolActionOwnershipTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"lc-tool-action-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    // ── Routing predicate ─────────────────────────────────────────────────────

    [Fact]
    public void TryGetActionOwnerTool_ReturnsFalse_ForNullAction()
    {
        Assert.False(GameClient.TryGetActionOwnerTool(null!, out var toolId));
        Assert.Equal(Guid.Empty, toolId);
    }

    [Fact]
    public void TryGetActionOwnerTool_ReturnsFalse_ForGameAction()
    {
        var action = new ManifestAction { Name = "Play", Path = "game.exe" };

        Assert.False(GameClient.TryGetActionOwnerTool(action, out var toolId));
        Assert.Equal(Guid.Empty, toolId);
    }

    /// <summary>
    /// An empty ToolId must not route to a tool — doing so would skip the game's scripts in favour
    /// of a tool that cannot be resolved.
    /// </summary>
    [Fact]
    public void TryGetActionOwnerTool_ReturnsFalse_ForEmptyToolId()
    {
        var action = new ManifestAction { Name = "Configure", ToolId = Guid.Empty };

        Assert.False(GameClient.TryGetActionOwnerTool(action, out _));
    }

    [Fact]
    public void TryGetActionOwnerTool_ReturnsToolId_ForToolAction()
    {
        var toolId = Guid.NewGuid();
        var action = new ManifestAction { Name = "Configure", ToolId = toolId };

        Assert.True(GameClient.TryGetActionOwnerTool(action, out var resolved));
        Assert.Equal(toolId, resolved);
    }

    // ── Manifest round-trip and back-compat ───────────────────────────────────

    [Fact]
    public async Task ToolManifest_RoundTrips_ActionToolId()
    {
        var toolId = Guid.NewGuid();
        var manifest = new ManifestTool
        {
            Id = toolId,
            Name = "Config Editor",
            Actions = new List<ManifestAction>
            {
                new() { Name = "Edit Config", Path = "edit.exe", ToolId = toolId },
            },
        };

        await ManifestHelper.WriteAsync(manifest, _tempDir);
        var read = await ManifestHelper.ReadAsync<ManifestTool>(_tempDir, toolId);

        Assert.Equal(toolId, read.Actions.Single().ToolId);
    }

    /// <summary>
    /// Manifests written before ToolId existed must still deserialize. The launcher stamps ownership
    /// client-side when merging tool actions precisely so those keep working without a reinstall.
    /// </summary>
    [Fact]
    public void LegacyToolManifest_WithoutToolId_DeserializesWithNullToolId()
    {
        var yaml = string.Join("\n",
            "Id: 4a5b6c7d-8e9f-4a1b-8c2d-3e4f5a6b7c8d",
            "Name: Config Editor",
            "Actions:",
            "- Name: Edit Config",
            "  Path: edit.exe");

        var manifest = ManifestHelper.Deserialize<ManifestTool>(yaml);

        Assert.Null(manifest.Actions.Single().ToolId);
    }

    /// <summary>
    /// A game action must never look tool-owned, or the game's own scripts would be skipped. The
    /// field is also omitted from the serialized form so game manifests and .lcx files are unchanged.
    /// </summary>
    [Fact]
    public async Task GameManifest_OmitsToolId_AndReadsBackNull()
    {
        var gameId = Guid.NewGuid();
        var manifest = new ManifestGame
        {
            Id = gameId,
            Title = "Test Game",
            Version = "1.0.0",
            Actions = new List<ManifestAction> { new() { Name = "Play", Path = "game.exe" } },
        };

        var path = await ManifestHelper.WriteAsync(manifest, _tempDir);

        Assert.DoesNotContain("ToolId", await File.ReadAllTextAsync(path));
        Assert.Null((await ManifestHelper.ReadAsync<ManifestGame>(_tempDir, gameId)).Actions.Single().ToolId);
    }
}
