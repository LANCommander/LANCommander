using LANCommander.Launcher.Data;
using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Launcher.Services
{
    public class MediaService : BaseDatabaseService<Media>
    {
        private readonly Settings Settings;

        public MediaService(DatabaseContext dbContext, SDK.Client client, ILogger<CollectionService> logger) : base(dbContext, client, logger)
        {
            Settings = SettingService.GetSettings();
        }

        public override Task Delete(Media entity)
        {
            DeleteLocalMediaFile(entity);

            return base.Delete(entity);
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
            var entity = await Get(id);

            return GetImagePath(entity);
        }

        public static string GetStoragePath()
        {
            var settings = SettingService.GetSettings();

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, settings.Media.StoragePath);
        }

        public static string GetImagePath(Media entity)
        {
            if (entity == null)
                return "";

            return Path.Combine(GetStoragePath(), $"{entity.FileId}-{entity.Crc32}");
        }

        public void DeleteLocalMediaFile(Media entity)
        {
            var path = GetImagePath(entity);

            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
