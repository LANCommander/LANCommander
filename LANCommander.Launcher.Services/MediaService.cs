using LANCommander.Launcher.Data;
using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Models;
using Microsoft.Extensions.Logging;
using LANCommander.SDK;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Services;

namespace LANCommander.Launcher.Services
{
    public class MediaService(
        ILogger<MediaService> logger,
        DatabaseContext dbContext,
        MediaClient mediaClient,
        SettingsProvider<Settings.Settings> settingsProvider) : BaseDatabaseService<Media>(dbContext, logger)
    {
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

        public string GetStoragePath()
        {
            return Path.Combine(AppPaths.GetConfigDirectory(), settingsProvider.CurrentValue.Media.StoragePath);
        }

        public string GetImagePath(Media entity)
        {
            if (entity == null)
                return "";

            return Path.Combine(GetStoragePath(), $"{entity.FileId}-{entity.Crc32}");
        }

        public void DeleteLocalMediaFile(Media entity)
        {
            using (var op = Logger.BeginOperation("Deleting local media file"))
            {
                op.Enrich("Id", entity.Id);
                
                try
                {
                    var path = GetImagePath(entity);

                    op.Enrich("Path", path);

                    if (File.Exists(path))
                        File.Delete(path);
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "An unknown error occurred while trying to delete a local file");
                }
            }
        }

        public async Task<FileInfo> DownloadAsync(Media entity)
        {
            var path = GetImagePath(entity);

            return await mediaClient.DownloadAsync(new SDK.Models.Media
            {
                Id = entity.Id,
                FileId = entity.FileId,
                Crc32 = entity.Crc32,
                Name = entity.Name,
                MimeType = entity.MimeType,
                Type = entity.Type,
            }, path);
        }
    }
}
