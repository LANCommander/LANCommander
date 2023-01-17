using LANCommander.Data;
using LANCommander.Data.Models;
using LANCommander.Helpers;

namespace LANCommander.Services
{
    public class GameSaveService : BaseDatabaseService<GameSave>
    {
        private readonly SettingService SettingService;

        public GameSaveService(DatabaseContext dbContext, IHttpContextAccessor httpContextAccessor, SettingService settingService) : base(dbContext, httpContextAccessor)
        {
            SettingService = settingService;
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
                return null;;

            return GetSavePath(save);
        }

        public string GetSavePath(GameSave save)
        {
            return Path.Combine("Save", save.UserId.ToString(), $"{save.Id}.zip");
        }
    }
}
