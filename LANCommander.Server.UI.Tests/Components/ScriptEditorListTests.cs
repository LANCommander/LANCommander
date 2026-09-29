using Bunit;
using LANCommander.SDK.Enums;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using LANCommander.Server.UI.Controls;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>
/// The ScriptEditor's list of a game's (or redistributable/server/tool's) scripts. The report
/// behind these tests is administrators opening a game's Scripts section and seeing no scripts even
/// though the game has some.
/// </summary>
[Collection("BUnit")]
public class ScriptEditorListTests(BUnitServerFixture fixture) : BUnitTestContext(fixture)
{
    private IRenderedComponent<ScriptEditor> RenderForGame(Guid gameId)
        => Render<ScriptEditor>(parameters => parameters
            .AddCascadingValue("GameId", (Guid?)gameId)
            .Add(p => p.AllowedTypes, new[] { ScriptType.Install, ScriptType.Uninstall }));

    private async Task<Guid> AddGameAsync()
    {
        using var scope = Fixture.Factory.RealServices.CreateScope();
        var gameService = scope.ServiceProvider.GetRequiredService<GameService>();

        var game = await gameService.AddAsync(new Game
        {
            Title = $"Scripts {Guid.NewGuid():N}",
            Type = GameType.MainGame,
        });

        return game.Id;
    }

    private async Task<Script> AddScriptViaServiceAsync(Guid gameId, string name)
    {
        using var scope = Fixture.Factory.RealServices.CreateScope();
        var scriptService = scope.ServiceProvider.GetRequiredService<ScriptService>();

        return await scriptService.AddAsync(new Script
        {
            Name = name,
            Type = ScriptType.Install,
            Contents = "# hi",
            GameId = gameId,
        });
    }

    private static List<string> ScriptNames(IRenderedComponent<ScriptEditor> editor) =>
        editor.FindAll("tbody tr.rz-data-row td:first-child").Select(td => td.TextContent.Trim()).ToList();

    [Fact]
    public async Task ListsAScriptWithNoVersion()
    {
        var gameId = await AddGameAsync();

        var factory = Fixture.Factory.RealServices.GetRequiredService<IDbContextFactory<DatabaseContext>>();
        await using (var context = await factory.CreateDbContextAsync())
        {
            context.Add(new Script
            {
                Id = Guid.NewGuid(),
                Name = "Legacy Script",
                Type = ScriptType.Install,
                Contents = "# legacy",
                GameId = gameId,
                GameVersionId = null,
            });
            await context.SaveChangesAsync();
        }

        var editor = RenderForGame(gameId);

        editor.WaitForAssertion(() =>
        {
            Assert.Contains("Legacy Script", ScriptNames(editor));
        }, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task ListsAGamesScripts()
    {
        var gameId = await AddGameAsync();
        await AddScriptViaServiceAsync(gameId, "My Install Script");

        var editor = RenderForGame(gameId);

        editor.WaitForAssertion(() =>
        {
            Assert.Contains("My Install Script", ScriptNames(editor));
        }, TimeSpan.FromSeconds(10));
    }
}
