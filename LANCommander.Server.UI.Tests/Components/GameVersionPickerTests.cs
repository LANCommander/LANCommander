using Bunit;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using LANCommander.Server.UI.Pages.Games.Edit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>
/// The game editor's header carries the version everything version-bound works on: it's on every
/// section, keeps an older version across sections, and warns while an older version is edited.
/// </summary>
[Collection("BUnit")]
public class GameVersionPickerTests : BUnitTestContext
{
    public GameVersionPickerTests(BUnitServerFixture fixture) : base(fixture)
    {
    }

    /// <summary>A game of its own with versions 1.0 and 2.0, so the shared test game is untouched.</summary>
    private async Task<(Game Game, GameVersion V1, GameVersion V2)> SeedAsync()
    {
        var game = await Services.GetRequiredService<GameService>().AddAsync(new Game { Title = $"Versioned {Guid.NewGuid():N}" });
        var versionService = Services.GetRequiredService<GameVersionService>();

        var v1 = await versionService.CreateAsync(game.Id, "1.0");
        var v2 = await versionService.CreateAsync(game.Id, "2.0");

        return (game, v1, v2);
    }

    [Fact]
    public async Task Header_ShowsTheSelectedVersionsStatus()
    {
        var (game, v1, _) = await SeedAsync();

        // Creating 2.0 published 1.0; 2.0 is a draft
        NavigateTo($"/Games/{game.Id}/Versions");

        var cut = Render<Versions>(parameters => parameters.Add(p => p.Id, game.Id));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("2.0 (latest)", cut.Find(".lc-page-header [aria-label='Game version']").TextContent);
            Assert.Contains(cut.FindAll(".lc-page-header-tags .lc-tag"), t => t.TextContent.Trim() == "Draft");
            Assert.Contains(cut.FindAll(".lc-page-header-tags button"), b => b.TextContent.Trim() == "Publish");
        }, TimeSpan.FromSeconds(10));

        NavigateTo($"/Games/{game.Id}/Versions?version={v1.Id}");

        var older = Render<Versions>(parameters => parameters.Add(p => p.Id, game.Id));

        older.WaitForAssertion(() =>
        {
            Assert.Contains(older.FindAll(".lc-page-header-tags .lc-tag"), t => t.TextContent.Trim() == "Published");
            Assert.Contains(older.FindAll(".lc-page-header-tags button"), b => b.TextContent.Trim() == "Unpublish");
        }, TimeSpan.FromSeconds(10));
    }

    private void NavigateTo(string uri) => Services.GetRequiredService<NavigationManager>().NavigateTo(uri);

    [Fact]
    public async Task Picker_IsInTheHeaderOfNonVersionedSections()
    {
        var (game, _, _) = await SeedAsync();

        NavigateTo($"/Games/{game.Id}/Media");

        var cut = Render<LANCommander.Server.UI.Pages.Games.Edit.Media>(parameters => parameters.Add(p => p.Id, game.Id));

        cut.WaitForAssertion(() =>
        {
            var picker = cut.Find(".lc-page-header [aria-label='Game version']");

            Assert.Contains("2.0 (latest)", picker.TextContent);
        }, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task OlderVersion_ShowsWarning_AndStaysSelectedAcrossSections()
    {
        var (game, v1, _) = await SeedAsync();

        NavigateTo($"/Games/{game.Id}/Scripts?version={v1.Id}");

        var cut = Render<Scripts>(parameters => parameters.Add(p => p.Id, game.Id));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Editing version 1.0, an older version", cut.Markup);

            // Section links keep the older version selected
            Assert.Contains(cut.FindAll("a"), a => a.GetAttribute("href") == $"/Games/{game.Id}/Actions?version={v1.Id}");
        }, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task LatestVersion_HasNoWarning()
    {
        var (game, _, _) = await SeedAsync();

        NavigateTo($"/Games/{game.Id}/Scripts");

        var cut = Render<Scripts>(parameters => parameters.Add(p => p.Id, game.Id));

        cut.WaitForAssertion(() => Assert.Contains("2.0 (latest)", cut.Find(".lc-page-header [aria-label='Game version']").TextContent), TimeSpan.FromSeconds(10));

        Assert.DoesNotContain("an older version", cut.Markup);
        Assert.Contains(cut.FindAll("a"), a => a.GetAttribute("href") == $"/Games/{game.Id}/Actions");
    }

    [Fact]
    public async Task VersionsTable_MarksTheVersionBeingEdited()
    {
        var (game, v1, _) = await SeedAsync();

        NavigateTo($"/Games/{game.Id}/Versions?version={v1.Id}");

        var cut = Render<Versions>(parameters => parameters.Add(p => p.Id, game.Id));

        cut.WaitForAssertion(() =>
        {
            var editing = cut.FindAll("tr").Where(r => r.TextContent.Contains("Editing")).ToList();

            Assert.Single(editing);
            Assert.Contains("1.0", editing[0].TextContent);
        }, TimeSpan.FromSeconds(10));
    }
}
