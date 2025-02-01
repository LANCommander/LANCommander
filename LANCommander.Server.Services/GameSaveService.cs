using AutoMapper;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Helpers;
using LANCommander.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class GameSaveService(
        ILogger<GameSaveService> logger,
        IFusionCache cache,
        IMapper mapper,
        IDbContextFactory<DatabaseContext> contextFactory) : BaseDatabaseService<GameSave>(logger, cache, mapper, contextFactory)
    {
        public override Task<GameSave> UpdateAsync(GameSave entity)
        {
            throw new NotImplementedException();
        }
        
        public override async Task DeleteAsync(GameSave entity)
        {
            FileHelpers.DeleteIfExists(await GetSavePathAsync(entity.Id));

            await base.DeleteAsync(entity);
        }
        
        public async Task<string> GetSavePathAsync(Guid gameId, Guid userId)
        {
            var save = await SortBy(gs => gs.CreatedOn, Data.Enums.SortDirection.Descending).FirstOrDefaultAsync(gs => gs.GameId == gameId && gs.UserId == userId);

            if (save == null)
                return null;

            return GetSavePath(save);
        }

        public async Task<string> GetSavePathAsync(Guid id)
        {
            // Use get with predicate to avoid async
            var save = await FirstOrDefaultAsync(gs => gs.Id == id);

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
