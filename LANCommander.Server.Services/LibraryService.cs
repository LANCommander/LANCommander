using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.Extensions.Logging;
using AutoMapper;
using LANCommander.Server.Services.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class LibraryService(
        ILogger<LibraryService> logger,
        IFusionCache cache,
        IMapper mapper,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> contextFactory,
        GameService gameService,
        UserService userService) : BaseDatabaseService<Library>(logger, cache, mapper, httpContextAccessor, contextFactory)
    {
        public override async Task<Library> AddAsync(Library entity)
        {
            return await base.AddAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(l => l.Games);
                await context.UpdateRelationshipAsync(l => l.User);
            });
        }

        public override async Task<Library> UpdateAsync(Library entity)
        {
            return await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(l => l.Games);
                await context.UpdateRelationshipAsync(l => l.User);
            });
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
            await cache.RemoveByTagAsync([$"Library/{userId}"]);

            if (game.BaseGame != null && !library.Games.Any(g => g.Id == game.BaseGame.Id) && game.BaseGame.Id != game.Id)
                await AddToLibraryAsync(userId, game.BaseGame.Id);
        }

        public async Task RemoveFromLibraryAsync(Guid userId, Guid gameId)
        {
            var library = await GetByUserIdAsync(userId);

            var game = library.Games.FirstOrDefault(g => g.Id == gameId);

            if (game != null && game.DependentGames != null && game.DependentGames.Any())
            {
                foreach (var dependentGame in game.DependentGames)
                {
                    if (library.Games.Any(g => g.Id == dependentGame.Id) && dependentGame.Id != game.Id)
                        library.Games.Remove(dependentGame);
                }
            }

            library.Games.Remove(game!);

            await UpdateAsync(library);

            await cache.RemoveByTagAsync([$"Library/{userId}"]);
        }
    }
}
