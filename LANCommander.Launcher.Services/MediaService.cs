using LANCommander.Launcher.Data;
using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Models;
using Microsoft.Extensions.Logging;
using LANCommander.SDK;

namespace LANCommander.Launcher.Services
{
    public class MediaService(
        ILogger<MediaService> logger,
        DatabaseContext dbContext) : BaseDatabaseService<Media>(dbContext, logger)
    {
        private readonly Settings Settings = SettingService.GetSettings();

        public override Task DeleteAsync(Media entity)
        {
            DeleteLocalMediaFile(entity);

            return base.DeleteAsync(entity);
        }

        public bool FileExists(Media entity)
        {
            var path = GetImagePath(entity);

            return File.Exists(path);
        }

        public async Task<bool> FileExists(Guid id)
        {
            var path = await GetImagePath(id);

            return File.Exists(path);
        }

        public async Task<string> GetImagePath(Guid id)
        {
            var entity = await GetAsync(id);

            return GetImagePath(entity);
        }

        public static string GetStoragePath()
        {
            var settings = SettingService.GetSettings();

            return Path.Combine(AppPaths.GetConfigDirectory(), settings.Media.StoragePath);
        }

        public static string GetImagePath(Media entity)
        {
            if (entity == null)
                return "";

            return Path.Combine(GetStoragePath(), $"{entity.FileId}-{entity.Crc32}");
        }

        public void DeleteLocalMediaFile(Media entity)
        {
            try
            {
                var path = GetImagePath(entity);

                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "An unknown error occurred while trying to delete a local file");
            }
        }
    }
}
