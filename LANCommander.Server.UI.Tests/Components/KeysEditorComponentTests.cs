using Bunit;
using LANCommander.SDK.Enums;
using LANCommander.Server.Services;
using LANCommander.Server.UI.Pages.Games.Components;
using KeysPage = LANCommander.Server.UI.Pages.Games.Edit.Keys;
using Microsoft.Extensions.DependencyInjection;
using DataGame = LANCommander.Server.Data.Models.Game;
using DataKey = LANCommander.Server.Data.Models.Key;
using DataUser = LANCommander.Server.Data.Models.User;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>
/// bUnit coverage for the admin Keys editor. The reports behind these tests are administrators
/// seeing a game's entire key pool marked as taken, and key rows appearing to belong to the wrong
/// game, so the assertions are about what the editor reads and when it re-reads it.
/// </summary>
[Collection("BUnit")]
public class KeysEditorComponentTests : BUnitTestContext
{
    public KeysEditorComponentTests(BUnitServerFixture fixture) : base(fixture)
    {
    }

    private IRenderedComponent<KeysEditor> RenderEditor(Guid gameId)
        => Render<KeysEditor>(parameters => parameters
            .AddCascadingValue("GameId", gameId));

    private async Task<DataGame> AddGameWithKeysAsync(int keyCount)
    {
        using var scope = Fixture.Factory.RealServices.CreateScope();

        var gameService = scope.ServiceProvider.GetRequiredService<GameService>();
        var keyService = scope.ServiceProvider.GetRequiredService<KeyService>();

        var game = await gameService.AddAsync(new DataGame
        {
            Title = $"Keys {Guid.NewGuid():N}",
            KeyAllocationMethod = KeyAllocationMethod.UserAccount,
        });

        for (var i = 0; i < keyCount; i++)
            await keyService.AddAsync(new DataKey
            {
                Value = $"{game.Id:N}-KEY-{i:D3}",
                GameId = game.Id,
            });

        return game;
    }

    private async Task AllocateFirstKeyAsync(Guid gameId)
    {
        using var scope = Fixture.Factory.RealServices.CreateScope();

        var keyService = scope.ServiceProvider.GetRequiredService<KeyService>();
        var userService = scope.ServiceProvider.GetRequiredService<UserService>();

        var user = await userService.AddAsync(
            new DataUser { UserName = $"holder{Guid.NewGuid():N}" },
            bypassPasswordPolicy: true,
            password: "Test1234!");

        var key = (await keyService.GetAsync(k => k.GameId == gameId)).First();

        await keyService.AllocateAsync(key, user);
    }

    /// <summary>
    /// The Available/Allocated/Total statistics have to agree with the state the grid below them
    /// shows. They are computed from a read that has to load the claimant navigation, because that
    /// is what marks a user-account key as allocated.
    /// </summary>
    [Fact]
    public async Task KeysEditor_CountsAnAllocatedKeyAsAllocated()
    {
        var game = await AddGameWithKeysAsync(4);

        await AllocateFirstKeyAsync(game.Id);

        var cut = RenderEditor(game.Id);

        var statistics = cut.FindAll(".ant-statistic")
            .Select(s => (
                Title: s.QuerySelector(".ant-statistic-title")?.TextContent?.Trim(),
                Value: s.QuerySelector(".ant-statistic-content-value")?.TextContent?.Trim()))
            .ToList();

        Assert.Equal("4", statistics.Single(s => s.Title == "Total").Value);
        Assert.Equal("1", statistics.Single(s => s.Title == "Allocated").Value);
        Assert.Equal("3", statistics.Single(s => s.Title == "Available").Value);
    }

    /// <summary>
    /// Both games' Keys pages are the same route, so moving between them re-uses the component
    /// instance and only updates the cascaded game id. The editor has to re-read its keys on that
    /// change: the Save handler diffs the edited text against the keys it is holding and writes
    /// the result under the *current* game id, so a stale list writes one game's keys onto
    /// another.
    /// </summary>
    [Fact]
    public async Task KeysEditor_RereadsItsKeysWhenTheGameChanges()
    {
        var first = await AddGameWithKeysAsync(2);
        var second = await AddGameWithKeysAsync(5);

        // Render the page rather than the editor directly: /Games/{id}/Keys is one route, so
        // switching games only changes this parameter and the component instance is re-used.
        var cut = Render<KeysPage>(parameters => parameters.Add(p => p.Id, first.Id));

        cut.Render(parameters => parameters.Add(p => p.Id, second.Id));

        var total = cut.FindAll(".ant-statistic")
            .Select(s => (
                Title: s.QuerySelector(".ant-statistic-title")?.TextContent?.Trim(),
                Value: s.QuerySelector(".ant-statistic-content-value")?.TextContent?.Trim()))
            .Single(s => s.Title == "Total");

        Assert.Equal("5", total.Value);
    }
}
