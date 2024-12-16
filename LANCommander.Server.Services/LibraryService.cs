using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public class LibraryService : BaseDatabaseService<Library>
    {
        private readonly UserService UserService;
        private readonly GameService GameService;

        public LibraryService(
            ILogger<LibraryService> logger,
            IFusionCache cache,
            RepositoryFactory repositoryFactory,
            UserService userService,
            GameService gameService) : base(logger, cache, repositoryFactory)
        {
            UserService = userService;
            GameService = gameService;
        }

        public async Task<Library> GetByUserIdAsync(Guid userId)
        {
            var library = await Include(l => l.Games).FirstOrDefaultAsync(l => l.User.Id == userId);

            if (library == null)
            {
                var user = await UserService.GetAsync(userId);

                if (user == null)
                    throw new Exception("User not found with ID " + userId.ToString());

                library = await AddAsync(new Library { UserId = userId });
            }

            return library;
        }

        public async Task AddToLibraryAsync(Guid userId, Guid gameId)
        {
            var game = await GameService.GetAsync(gameId);

            var library = await GetByUserIdAsync(userId);

            library.Games.Add(game);

            await UpdateAsync(library);
            await Cache.ExpireAsync($"LibraryGames:{userId}");

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

            await Cache.ExpireAsync($"LibraryGames:{userId}");
        }
    }
}
