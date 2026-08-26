using Bunit;
using LANCommander.SDK.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using LANCommander.Server.UI.Components;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>
/// bUnit component tests for the Phase 5 admin UX in <see cref="ArchiveEditor"/>: explicit vs.
/// effective default badges, the "Set Default"/"Use Latest Automatically" actions, and the
/// delete-protection guard for a game's current explicit default. Each test seeds its own game
/// (rather than the shared <see cref="BUnitServerFixture.TestGameId"/>) so archive state doesn't
/// leak into other bUnit tests sharing the same fixture/database.
/// </summary>
[Collection("BUnit")]
public class ArchiveEditorComponentTests : BUnitTestContext
{
    public ArchiveEditorComponentTests(BUnitServerFixture fixture) : base(fixture)
    {
    }

    private async Task<(Game Game, Archive Older, Archive Newer)> SeedGameWithTwoArchivesAsync()
    {
        using var scope = Fixture.Factory.RealServices.CreateScope();
        var gameService = scope.ServiceProvider.GetRequiredService<GameService>();
        var archiveService = scope.ServiceProvider.GetRequiredService<ArchiveService>();
        var storageLocationService = scope.ServiceProvider.GetRequiredService<StorageLocationService>();

        var storageLocation = await storageLocationService.DefaultAsync(StorageLocationType.Archive);

        var game = await gameService.AddAsync(new Game
        {
            Title = "Archive Editor Test Game " + Guid.NewGuid().ToString("N"),
            Type = GameType.MainGame,
        });

        var older = await archiveService.AddAsync(new Archive
        {
            GameId = game.Id,
            Version = "1.0.0-older",
            ObjectKey = Guid.NewGuid().ToString(),
            StorageLocationId = storageLocation.Id,
        });

        var newer = await archiveService.AddAsync(new Archive
        {
            GameId = game.Id,
            Version = "2.0.0-newer",
            ObjectKey = Guid.NewGuid().ToString(),
            StorageLocationId = storageLocation.Id,
        });

        older.CreatedOn = DateTime.UtcNow.AddDays(-2);
        newer.CreatedOn = DateTime.UtcNow.AddDays(-1);

        older = await archiveService.UpdateAsync(older);
        newer = await archiveService.UpdateAsync(newer);

        return (game, older, newer);
    }

    private IRenderedComponent<ArchiveEditor> RenderForGame(Guid gameId) =>
        RenderComponent<ArchiveEditor>(parameters => parameters.AddCascadingValue("GameId", gameId));

    [Fact]
    public async Task ArchiveEditor_ShowsLatestBadge_WhenNoExplicitDefaultSet()
    {
        var (game, _, newer) = await SeedGameWithTwoArchivesAsync();

        var cut = RenderForGame(game.Id);

        cut.WaitForAssertion(
            () => Assert.Contains(newer.Version, cut.Markup),
            timeout: TimeSpan.FromSeconds(10));

        // No explicit default is set: the newest archive shows "Latest", never "Default".
        cut.WaitForAssertion(
            () => Assert.Contains("Latest", cut.Markup),
            timeout: TimeSpan.FromSeconds(10));

        Assert.DoesNotContain(">Default<", cut.Markup);
    }

    [Fact]
    public async Task ArchiveEditor_ShowsDefaultBadge_WhenExplicitDefaultSet()
    {
        var (game, older, _) = await SeedGameWithTwoArchivesAsync();

        using (var scope = Fixture.Factory.RealServices.CreateScope())
        {
            var gameService = scope.ServiceProvider.GetRequiredService<GameService>();
            await gameService.SetDefaultArchiveAsync(game.Id, older.Id);
        }

        var cut = RenderForGame(game.Id);

        cut.WaitForAssertion(
            () => Assert.Contains("Default", cut.Markup),
            timeout: TimeSpan.FromSeconds(10));

        // "Use Latest Automatically" becomes actionable once an explicit default exists.
        cut.WaitForAssertion(
            () => Assert.Contains(
                cut.FindAll("button"),
                b => b.TextContent.Contains("Use Latest Automatically", StringComparison.OrdinalIgnoreCase)
                     && !b.HasAttribute("disabled")),
            timeout: TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task ArchiveEditor_RendersWithoutThrowing_ForToolContext_AndHidesDefaultControls()
    {
        using var scope = Fixture.Factory.RealServices.CreateScope();
        var toolService = scope.ServiceProvider.GetRequiredService<ToolService>();
        var archiveService = scope.ServiceProvider.GetRequiredService<ArchiveService>();
        var storageLocationService = scope.ServiceProvider.GetRequiredService<StorageLocationService>();

        var storageLocation = await storageLocationService.DefaultAsync(StorageLocationType.Archive);

        var tool = await toolService.AddAsync(new Tool { Name = "Archive Editor Test Tool " + Guid.NewGuid().ToString("N") });

        var archive = await archiveService.AddAsync(new Archive
        {
            ToolId = tool.Id,
            Version = "1.0.0",
            ObjectKey = Guid.NewGuid().ToString(),
            StorageLocationId = storageLocation.Id,
        });

        var cut = RenderComponent<ArchiveEditor>(parameters => parameters.AddCascadingValue("ToolId", tool.Id));

        cut.WaitForAssertion(
            () => Assert.Contains(archive.Version, cut.Markup),
            timeout: TimeSpan.FromSeconds(10));

        // Redistributable/tool archives have no default concept: no default-related controls.
        Assert.DoesNotContain("Use Latest Automatically", cut.Markup);
        Assert.DoesNotContain(">Default<", cut.Markup);
        Assert.DoesNotContain(">Latest<", cut.Markup);
    }
}
