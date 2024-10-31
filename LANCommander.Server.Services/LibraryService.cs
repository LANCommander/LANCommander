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
            Repository<Library> repository,
            UserService userService,
            GameService gameService) : base(logger, cache, repository)
        {
            UserService = userService;
            GameService = gameService;
        }

        public async Task<IEnumerable<Game>> Get(Guid userId)
        {
            var library = await FirstOrDefault(l => l.User.Id == userId);

            return library.Games;
        }

        public async Task AddToLibrary(Guid userId, Guid gameId)
        {
            var game = await GameService.Get(gameId);

            var library = await FirstOrDefault(l => l.User.Id == userId);

            library.Games.Add(game);

            await Update(library);
        }

        public async Task RemoveFromLibrary(Guid userId, Guid gameId)
        {
            var library = await FirstOrDefault(l => l.User.Id == userId);

            var game = library.Games.FirstOrDefault(g => g.Id == gameId);

            library.Games.Remove(game);

            await Update(library);
        }
    }
}
