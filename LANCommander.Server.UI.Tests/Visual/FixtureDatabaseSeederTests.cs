using LANCommander.Server.Data;
using LANCommander.Server.UI.Fixtures.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.UI.Tests.Visual;

/// <summary>
/// The visual baselines are only as stable as the data behind them. These pin down what the seeder
/// produces and that nothing in it depends on when or in what order it ran.
/// </summary>
[Collection("Visual")]
public class FixtureDatabaseSeederTests(VisualServerFixture fixture)
{
    private async Task<T> QueryAsync<T>(Func<DatabaseContext, Task<T>> query)
    {
        using var scope = fixture.Factory.RealServices.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();

        await using var context = await factory.CreateDbContextAsync();

        return await query(context);
    }

    [Fact]
    public async Task SeedsEveryGame_WithRelationships()
    {
        var arenaBlitz = await QueryAsync(c => c.Games!
            .Include(g => g.Genres)
            .Include(g => g.Tags)
            .Include(g => g.Platforms)
            .Include(g => g.Developers)
            .Include(g => g.Publishers)
            .Include(g => g.Collections)
            .Include(g => g.Archives)
            .Include(g => g.Keys)
            .Include(g => g.Actions)
            .Include(g => g.DependentGames)
            .AsSplitQuery()
            .SingleAsync(g => g.Id == FixtureData.Games.Id(FixtureData.Games.ArenaBlitz)));

        Assert.Equal(["Action", "First-Person Shooter"], arenaBlitz.Genres!.Select(g => g.Name).Order());
        Assert.Equal(3, arenaBlitz.Tags!.Count);
        Assert.Equal("Windows", Assert.Single(arenaBlitz.Platforms!).Name);
        Assert.Equal("Pixel Forge", Assert.Single(arenaBlitz.Developers!).Name);
        Assert.Equal("Lantern Publishing", Assert.Single(arenaBlitz.Publishers!).Name);
        Assert.Equal("LAN Party Essentials", Assert.Single(arenaBlitz.Collections).Name);
        Assert.Equal(2, arenaBlitz.Archives!.Count);
        Assert.Equal(8, arenaBlitz.Keys!.Count);
        Assert.Equal(3, arenaBlitz.Keys!.Count(k => k.ClaimedByUserId != null));
        Assert.Equal(2, arenaBlitz.Actions!.Count);
        Assert.Equal(FixtureData.Games.ArenaBlitzMapPack, Assert.Single(arenaBlitz.DependentGames).Title);

        Assert.Equal(8, await QueryAsync(c => c.Games!.CountAsync()));
    }

    [Fact]
    public async Task SeedsSupportingData()
    {
        Assert.Equal(2, await QueryAsync(c => c.Redistributables!.CountAsync()));
        Assert.Equal(2, await QueryAsync(c => c.Set<LANCommander.Server.Data.Models.Tool>().CountAsync()));
        Assert.Equal(2, await QueryAsync(c => c.Servers!.CountAsync()));
        Assert.Equal(2, await QueryAsync(c => c.Issues!.CountAsync()));
        Assert.Equal(2, await QueryAsync(c => c.Pages!.CountAsync()));
        Assert.Equal(1 + FixtureData.PlayerUserNames.Length, await QueryAsync(c => c.Users!.CountAsync()));
        Assert.True(await QueryAsync(c => c.PlaySessions!.CountAsync()) > 0);
    }

    [Fact]
    public async Task SeedsAdministratorLibraryAndSaves()
    {
        var adminId = FixtureIds.For("user:" + FixtureData.AdminUserName);

        Assert.Equal(3, await QueryAsync(c => c.Set<LANCommander.Server.Data.Models.Library>().Where(l => l.UserId == adminId).SelectMany(l => l.Games).CountAsync()));
        Assert.Equal(3, await QueryAsync(c => c.Set<LANCommander.Server.Data.Models.GameSave>().CountAsync(gs => gs.UserId == adminId)));
    }

    [Fact]
    public async Task Timestamps_DerivedFromIds()
    {
        var games = await QueryAsync(c => c.Games!.AsNoTracking().ToListAsync());

        Assert.All(games, g =>
        {
            Assert.Equal(FixtureIds.CreatedOn(g.Id), g.CreatedOn);
            Assert.Equal(FixtureIds.UpdatedOn(g.Id), g.UpdatedOn);
            Assert.True(g.CreatedOn < FixtureIds.Epoch);
        });

        var users = await QueryAsync(c => c.Users!.AsNoTracking().ToListAsync());

        Assert.All(users, u => Assert.Equal(FixtureIds.CreatedOn(u.Id), u.CreatedOn));
    }
}
