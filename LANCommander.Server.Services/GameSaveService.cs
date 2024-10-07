using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Helpers;
using LANCommander.Server.Models;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services
{
    public class GameSaveService : BaseDatabaseService<GameSave>
    {
        public GameSaveService(
            ILogger<GameSaveService> logger,
            DatabaseContext dbContext) : base(logger, dbContext) { }

        public override Task Delete(GameSave entity)
        {
            FileHelpers.DeleteIfExists(GetSavePath(entity.Id));

            return base.Delete(entity);
        }

        public string GetSavePath(Guid gameId, Guid userId)
        {
            var save = Get(gs => gs.GameId == gameId && gs.UserId == userId).FirstOrDefault();

            if (save == null)
                return null;

            return GetSavePath(save.Id);
        }

        public string GetSavePath(Guid id)
        {
            // Use get with predicate to avoid async
            var save = Get(gs => gs.Id == id).FirstOrDefault();

            if (save == null)
                return null;

            return GetSavePath(save);
        }

        public string GetSavePath(GameSave save)
        {
            return Path.Combine(Settings.UserSaves.StoragePath, save.UserId.ToString(), save.GameId.ToString(), $"{save.Id}");
        }
    }
}
