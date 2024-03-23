using LANCommander.Data;
using LANCommander.Data.Models;
using LANCommander.Helpers;
using LANCommander.Models;

namespace LANCommander.Services
{
    public class GameSaveService : BaseDatabaseService<GameSave>
    {
        private readonly LANCommanderSettings Settings;

        public GameSaveService(DatabaseContext dbContext, IHttpContextAccessor httpContextAccessor) : base(dbContext, httpContextAccessor)
        {
            Settings = SettingService.GetSettings();
        }

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
