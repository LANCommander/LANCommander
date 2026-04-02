using AutoMapper;
using LANCommander.Server.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services;

public class DepotService(
    GameService gameService,
    CollectionService collectionService,
    CompanyService companyService,
    EngineService engineService,
    GenreService genreService,
    PlatformService platformService,
    TagService tagService,
    LibraryService libraryService,
    UserService userService,
    PlaySessionService playSessionService,
    RatingService ratingService,
    IMapper mapper,
    ILogger<DepotService> logger)
{
    public async Task<SDK.Models.DepotResults> GetResults()
    {
        var games = await gameService
            .AsNoTracking()
            .Include(g => g.Media)
            .Include(g => g.Collections)
            .Include(g => g.Platforms)
            .Include(g => g.Tags)
            .Include(g => g.Developers)
            .Include(g => g.Genres)
            .Include(g => g.Publishers)
            .Include(g => g.Engine)
            .GetAsync();

        var depotResults = new SDK.Models.DepotResults
        {
            Games = mapper.Map<ICollection<SDK.Models.DepotGame>>(games),
            Collections = mapper.Map<ICollection<SDK.Models.Collection>>(await collectionService.AsNoTracking().GetAsync()),
            Companies = mapper.Map<ICollection<SDK.Models.Company>>(await companyService.AsNoTracking().GetAsync()),
            Engines = mapper.Map<ICollection<SDK.Models.Engine>>(await engineService.AsNoTracking().GetAsync()),
            Genres = mapper.Map<ICollection<SDK.Models.Genre>>(await genreService.AsNoTracking().GetAsync()),
            Platforms = mapper.Map<ICollection<SDK.Models.Platform>>(await platformService.AsNoTracking().GetAsync()),
            Tags = mapper.Map<ICollection<SDK.Models.Tag>>(await tagService.AsNoTracking().GetAsync()),
        };

        return depotResults;
    }

    public async Task<Game> GetGameAsync(Guid gameId)
    {
        return await gameService
            .AsNoTracking()
            .Include(g => g.Actions)
            .Include(g => g.Archives)
            .Include(g => g.BaseGame)
            .Include(g => g.Categories)
            .Include(g => g.Collections)
            .Include(g => g.DependentGames)
            .Include(g => g.Developers)
            .Include(g => g.Engine)
            .Include(g => g.Genres)
            .Include(g => g.Media)
            .Include(g => g.MultiplayerModes)
            .Include(g => g.Platforms)
            .Include(g => g.Publishers)
            .Include(g => g.Redistributables)
            .Include(g => g.SavePaths)
            .Include(g => g.Scripts)
            .Include(g => g.Tags)
            .GetAsync(gameId);
    }

    public async Task<ICollection<Guid>> GetPopularGameIds()
    {
        var sessions = await playSessionService
            .AsNoTracking()
            .GetAsync(ps => ps.GameId.HasValue && ps.GameId.Value != Guid.Empty);

        var topGames = sessions
            .Where(ps => ps.Start.HasValue && ps.End.HasValue)
            .GroupBy(ps => ps.GameId!.Value)
            .Select(g => new
            {
                GameId = g.Key,
                TotalTime = g.Sum(x => (x.End!.Value - x.Start!.Value).Ticks)
            })
            .OrderByDescending(x => x.TotalTime)
            .Take(10)
            .Select(gpt => gpt.GameId)
            .ToList();

        return topGames;
    }
    
    public async Task<ICollection<Guid>> GetBacklogGameIds()
    {
        var sessions = await playSessionService
            .AsNoTracking()
            .GetAsync(ps => ps.GameId.HasValue && ps.GameId.Value != Guid.Empty);

        var ratings = await ratingService
            .AsNoTracking()
            .GetAsync(); // assuming r.GameId and r.Value exist

        var playtimeByGame = sessions
            .Where(ps => ps.Start.HasValue && ps.End.HasValue)
            .GroupBy(ps => ps.GameId!.Value)
            .Select(g => new
            {
                GameId = g.Key,
                TotalTicks = g.Sum(x => (x.End!.Value - x.Start!.Value).Ticks)
            });

        var ratingByGame = ratings
            .GroupBy(r => r.GameId)
            .Select(g => new
            {
                GameId = g.Key,
                AvgRating = g.Average(x => x.Value)
            });

        var query =
            from p in playtimeByGame
            join r in ratingByGame on p.GameId equals r.GameId
            select new
            {
                p.GameId,
                r.AvgRating,
                PlaytimeTicks = p.TotalTicks,
                Score = r.AvgRating / (p.TotalTicks + 1)
            };

        var result = query
            .OrderByDescending(x => x.Score)
            .Take(10)
            .Select(x => x.GameId)
            .ToList();

        return result;
    }
}