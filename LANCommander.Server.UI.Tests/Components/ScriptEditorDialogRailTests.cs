using Bunit;
using LANCommander.SDK.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Models;
using LANCommander.Server.Services;
using LANCommander.Server.UI.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>
/// The script editor dialog's rail surfaces for snippets, cmdlets and variables. The report behind
/// these tests is administrators being unable to view the available snippets and cmdlets: they were
/// reachable only through toolbar dropdowns whose popup does not open where it can be seen, and
/// cmdlets had no browse surface at all. Each is now listed as clickable chips in the rail, always
/// in the DOM (no popup), so this coverage renders the dialog and asserts they are present.
/// </summary>
[Collection("BUnit")]
public class ScriptEditorDialogRailTests(BUnitServerFixture fixture) : BUnitTestContext(fixture)
{
    private async Task<(Guid GameId, Guid ScriptId)> SeedAsync(bool withSnippet)
    {
        using var scope = Fixture.Factory.RealServices.CreateScope();

        var gameService = scope.ServiceProvider.GetRequiredService<GameService>();
        var scriptService = scope.ServiceProvider.GetRequiredService<ScriptService>();

        var game = await gameService.AddAsync(new Game { Title = "Rail " + Guid.NewGuid().ToString("N") });
        var script = await scriptService.AddAsync(new Script { GameId = game.Id, Name = "Install", Contents = "x", Type = ScriptType.Install });

        if (withSnippet)
            scriptService.SaveSnippet(new Snippet { Group = "General", Name = "Hello " + Guid.NewGuid().ToString("N"), Content = "Write-Host 'hi'" });

        return (game.Id, script.Id);
    }

    private IRenderedComponent<ScriptEditorDialog> RenderDialog(Guid gameId, Guid scriptId)
    {
        var cut = Render<ScriptEditorDialog>(parameters => parameters
            .Add(p => p.Options, new ScriptEditorOptions
            {
                ScriptId = scriptId,
                GameId = gameId,
                AllowedTypes = new[] { ScriptType.Install, ScriptType.Uninstall },
            }));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".script-editor-toolbar")), TimeSpan.FromSeconds(10));

        return cut;
    }

    [Fact]
    public async Task Rail_ListsVariablesAsClickableChips()
    {
        var (gameId, scriptId) = await SeedAsync(withSnippet: false);

        var cut = RenderDialog(gameId, scriptId);

        // The "Runs with" card lists the script type's variables as chips
        var chips = cut.FindAll(".script-editor-variables .script-editor-chip").Select(c => c.TextContent.Trim()).ToList();

        Assert.Contains("$InstallDirectory", chips);
    }

    [Fact]
    public async Task Rail_ListsCmdletsWhenModulesProvideThem()
    {
        var (gameId, scriptId) = await SeedAsync(withSnippet: false);

        bool hasFunctions;
        using (var scope = Fixture.Factory.RealServices.CreateScope())
        {
            var moduleService = scope.ServiceProvider.GetRequiredService<ModuleService>();
            hasFunctions = moduleService.GetPublicFunctionCompletions().Any();
        }

        var cut = RenderDialog(gameId, scriptId);

        var cmdletCard = cut.FindAll(".script-editor-cmdlets");

        // The card is present exactly when the modules provide functions to browse
        Assert.Equal(hasFunctions, cmdletCard.Count > 0);

        if (hasFunctions)
            Assert.NotEmpty(cut.FindAll(".script-editor-cmdlets .script-editor-chip"));
    }

    [Fact]
    public async Task Rail_ListsSnippetsAsClickableChips()
    {
        var (gameId, scriptId) = await SeedAsync(withSnippet: true);

        var cut = RenderDialog(gameId, scriptId);

        // Snippets are always browsable in the rail, not only behind the toolbar dropdown popup
        var snippetChips = cut.FindAll(".script-editor-snippets .script-editor-chip");

        Assert.NotEmpty(snippetChips);
    }
}
