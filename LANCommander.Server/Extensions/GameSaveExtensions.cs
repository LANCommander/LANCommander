using LANCommander.Server.Services;
using Steamworks.Data;

namespace LANCommander.Server.Extensions
{
    public static class GameSaveExtensions
    {
        public static string GetUploadPath(this Data.Models.GameSave gameSave)
        {
            return Path.Combine(gameSave.StorageLocation.Path, gameSave.UserId.ToString(), gameSave.GameId.ToString(), gameSave.Id.ToString());
        }
    }
}
