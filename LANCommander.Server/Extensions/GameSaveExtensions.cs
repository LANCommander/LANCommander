using LANCommander.Server.Services;
using Steamworks.Data;

namespace LANCommander.Server.Extensions
{
    public static class GameSaveExtensions
    {
        public static string GetUploadPath(this Data.Models.GameSave gameSave)
        {
            var settings = SettingService.GetSettings();

            return Path.Combine(settings.UserSaves.StoragePath, gameSave.UserId.ToString(), gameSave.GameId.ToString(), gameSave.Id.ToString());
        }

        public static string GetGameSaveUploadPath(this Data.Models.User user)
        {
            var settings = SettingService.GetSettings();

            return Path.Combine(settings.UserSaves.StoragePath, user.Id.ToString());
        }
    }
}
