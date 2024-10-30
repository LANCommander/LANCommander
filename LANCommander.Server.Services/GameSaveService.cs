using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Helpers;
using LANCommander.Server.Models;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public class GameSaveService : BaseDatabaseService<GameSave>
    {
        public GameSaveService(
            ILogger<GameSaveService> logger,
            IFusionCache cache,
            Repository<GameSave> repository) : base(logger, cache, repository) { }

        public override async Task Delete(GameSave entity)
        {
            FileHelpers.DeleteIfExists(await GetSavePath(entity.Id));

            await base.Delete(entity);
        }

        public async Task<string> GetSavePath(Guid gameId, Guid userId)
        {
            var save = await FirstOrDefault(gs => gs.GameId == gameId && gs.UserId == userId);

            if (save == null)
                return null;

            return GetSavePath(save);
        }

        public async Task<string> GetSavePath(Guid id)
        {
            // Use get with predicate to avoid async
            var save = await FirstOrDefault(gs => gs.Id == id);

            if (save == null)
                return null;

            return GetSavePath(save);
        }

        public string GetSavePath(GameSave save)
        {
            return Path.Combine(save.StorageLocation.Path, save.UserId.ToString(), save.GameId.ToString(), $"{save.Id}");
        }
    }
}
