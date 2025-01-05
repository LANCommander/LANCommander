using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Helpers;
using System.IO.Abstractions;

namespace LANCommander.Server.Services
{
    public class GameSaveService(
        ILogger<GameSaveService> logger,
        DatabaseContext dbContext,
        IHttpContextAccessor httpContextAccessor,
        IPath path) : BaseDatabaseService<GameSave>(logger, dbContext, httpContextAccessor)
    {
        public override Task Delete(GameSave entity)
        {
            string? savesPath = GetSavePath(entity.Id);

            if (savesPath is null)
            {
                return Task.CompletedTask;
            }

            FileHelpers.DeleteIfExists(savesPath);

            return base.Delete(entity);
        }

        public string? GetSavePath(Guid gameId, Guid userId)
        {
            var save = Get(gs => gs.GameId == gameId && gs.UserId == userId).FirstOrDefault();

            if (save == null)
                return null;

            return GetSavePath(save.Id);
        }

        public string? GetSavePath(Guid id)
        {
            // Use get with predicate to avoid async
            var save = Get(gs => gs.Id == id).FirstOrDefault();

            if (save == null)
                return null;

            return GetSavePath(save);
        }

        public string GetSavePath(GameSave save)
        {
            return path.Combine(Settings.UserSaves.StoragePath, save.UserId.ToString(), save.GameId.ToString(), $"{save.Id}");
        }
    }
}
