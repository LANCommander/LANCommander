using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.Extensions.Logging;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class LibraryService(
        ILogger<LibraryService> logger,
        IFusionCache cache,
        IMapper mapper,
        IDbContextFactory<DatabaseContext> contextFactory,
        GameService gameService,
        UserService userService) : BaseDatabaseService<Library>(logger, cache, mapper, contextFactory)
    {
        public override Task<Library> UpdateAsync(Library entity)
        {
            throw new NotImplementedException();
        }
        
        public async Task<Library> GetByUserIdAsync(Guid userId)
        {
            var library = await Include(l => l.Games).FirstOrDefaultAsync(l => l.User.Id == userId);

            if (library == null)
            {
                var user = await userService.GetAsync(userId);

                if (user == null)
                    throw new Exception("User not found with ID " + userId.ToString());

                library = await AddAsync(new Library { UserId = userId });
            }

            return library;
        }

        public async Task AddToLibraryAsync(Guid userId, Guid gameId)
        {
            var game = await gameService.GetAsync(gameId);

            var library = await GetByUserIdAsync(userId);

            library.Games.Add(game);

            await UpdateAsync(library);
            await cache.ExpireAsync($"LibraryGames:{userId}");

            if (game.BaseGame != null && !library.Games.Any(g => g.Id == game.BaseGame.Id) && game.BaseGame.Id != game.Id)
                await AddToLibraryAsync(userId, game.BaseGame.Id);
        }

        public async Task RemoveFromLibraryAsync(Guid userId, Guid gameId)
        {
            var library = await GetByUserIdAsync(userId);

            var game = library.Games.FirstOrDefault(g => g.Id == gameId);

            if (game.DependentGames != null && game.DependentGames.Any())
            {
                foreach (var dependentGame in game.DependentGames)
                {
                    if (library.Games.Any(g => g.Id == dependentGame.Id) && dependentGame.Id != game.Id)
                        await RemoveFromLibraryAsync(userId, dependentGame.Id);
                }
            }

            library.Games.Remove(game);

            await UpdateAsync(library);

            await cache.ExpireAsync($"LibraryGames:{userId}");
        }
    }
}
