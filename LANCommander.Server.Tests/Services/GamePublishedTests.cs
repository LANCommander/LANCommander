using System.Security.Claims;
using LANCommander.Server.Endpoints;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Mappers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using ZiggyCreatures.Caching.Fusion;
using DataGame = LANCommander.Server.Data.Models.Game;
using DataPlaySession = LANCommander.Server.Data.Models.PlaySession;
using DataUser = LANCommander.Server.Data.Models.User;

namespace LANCommander.Server.Tests.Services;

/// <summary>
/// Unpublished (hidden) games must not reach launcher users: the depot list, the depot game
/// lookup, the popular list and the library endpoints all leave them out.
/// </summary>
[Collection("Application")]
public class GamePublishedTests(ApplicationFixture fixture) : DalTest(fixture)
{
    private Task<DataGame> AddGameAsync(bool published) =>
        GetService<GameService>().AddAsync(new DataGame { Title = Unique("Game"), Published = published });

    private async Task<DataUser> AddUserAsync() =>
        await GetService<UserService>().AddAsync(
            new DataUser { UserName = Unique("player") },
            bypassPasswordPolicy: true,
            password: "Test1234!");

    [Fact]
    public void NewGamesArePublishedByDefault()
    {
        new DataGame().Published.ShouldBeTrue();
    }

    [Fact]
    public async Task DepotResultsExcludeUnpublishedGames()
    {
        var published = await AddGameAsync(true);
        var hidden = await AddGameAsync(false);

        var results = await GetService<DepotService>().GetResults();

        results.Games.ShouldContain(g => g.Id == published.Id);
        results.Games.ShouldNotContain(g => g.Id == hidden.Id);
    }

    [Fact]
    public async Task DepotGameLookupReturnsNullForUnpublishedGames()
    {
        var published = await AddGameAsync(true);
        var hidden = await AddGameAsync(false);

        var depotService = GetService<DepotService>();

        (await depotService.GetGameAsync(published.Id)).ShouldNotBeNull();
        (await depotService.GetGameAsync(hidden.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task PopularGamesExcludeUnpublishedGames()
    {
        var user = await AddUserAsync();
        var published = await AddGameAsync(true);
        var hidden = await AddGameAsync(false);

        var playSessionService = GetService<PlaySessionService>();
        var now = DateTime.UtcNow;

        // Far more playtime than anything else in the shared test database, so both would top the list
        await playSessionService.AddAsync(new DataPlaySession { GameId = hidden.Id, UserId = user.Id, Start = now.AddDays(-400), End = now.AddDays(-1) });
        await playSessionService.AddAsync(new DataPlaySession { GameId = published.Id, UserId = user.Id, Start = now.AddDays(-300), End = now.AddDays(-1) });

        var popular = await GetService<DepotService>().GetPopularGameIds();

        popular.ShouldContain(published.Id);
        popular.ShouldNotContain(hidden.Id);
    }

    [Fact]
    public async Task UnpublishedGameIdsTrackThePublishedFlag()
    {
        var gameService = GetService<GameService>();
        var game = await AddGameAsync(true);

        (await gameService.GetUnpublishedGameIdsAsync()).ShouldNotContain(game.Id);

        game.Published = false;
        await gameService.UpdateAsync(game);

        (await gameService.GetUnpublishedGameIdsAsync()).ShouldContain(game.Id);
    }

    [Fact]
    public async Task LibraryGamesExcludeUnpublishedGames()
    {
        var user = await AddUserAsync();
        var published = await AddGameAsync(true);
        var hidden = await AddGameAsync(false);

        await GetService<LibraryService>().AddToLibraryAsync(user.Id, published.Id);
        await GetService<LibraryService>().AddToLibraryAsync(user.Id, hidden.Id);

        await using var scope = ApplicationFixture.Instance.ServiceProvider.CreateAsyncScope();

        var result = await LibraryEndpoints.GetGamesAsync(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, user.UserName)], "Test")),
            scope.ServiceProvider.GetRequiredService<SdkMapper>(),
            scope.ServiceProvider.GetRequiredService<IFusionCache>(),
            scope.ServiceProvider.GetRequiredService<GameService>(),
            scope.ServiceProvider.GetRequiredService<LibraryService>(),
            scope.ServiceProvider.GetRequiredService<UserService>(),
            scope.ServiceProvider.GetRequiredService<SettingsProvider<Settings.Settings>>(),
            scope.ServiceProvider.GetRequiredService<ILoggerFactory>());

        var games = ((result as IValueHttpResult)?.Value as IEnumerable<SDK.Models.Game>)?.ToList();

        games.ShouldNotBeNull();
        games.ShouldContain(g => g.Id == published.Id);
        games.ShouldNotContain(g => g.Id == hidden.Id);
    }
}
