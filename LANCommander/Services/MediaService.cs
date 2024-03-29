﻿using Force.Crc32;
using LANCommander.Data;
using LANCommander.Data.Models;
using LANCommander.Helpers;
using LANCommander.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace LANCommander.Services
{
    public class MediaService : BaseDatabaseService<Media>
    {
        private readonly LANCommanderSettings Settings;

        public MediaService(DatabaseContext dbContext, IHttpContextAccessor httpContextAccessor) : base(dbContext, httpContextAccessor)
        {
            Settings = SettingService.GetSettings();
        }

        public override Task Delete(Media entity)
        {
            FileHelpers.DeleteIfExists(GetImagePath(entity));

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

        public static string GetImagePath(Media entity)
        {
            var settings = SettingService.GetSettings();

            return Path.Combine(settings.Media.StoragePath, entity.FileId.ToString());
        }

        public async Task<Media> UploadMediaAsync(IBrowserFile file, Media media)
        {
            var settings = SettingService.GetSettings();

            var fileId = Guid.NewGuid();

            var path = Path.Combine(Settings.Media.StoragePath, fileId.ToString());

            using (var fs = new FileStream(path, FileMode.Create))
            {
                await file.OpenReadStream(maxAllowedSize: settings.Media.MaxSize * 1024 * 1024).CopyToAsync(fs);
            }

            media.Crc32 = CalculateChecksum(path);
            media.FileId = fileId;

            return media;
        }

        public void DeleteLocalMediaFile(Guid fileId)
        {
            var path = Path.Combine(Settings.Media.StoragePath, fileId.ToString());

            if (File.Exists(path))
                File.Delete(path);
        }

        public async Task<Media> DownloadMediaAsync(string sourceUrl, Media media)
        {
            var fileId = Guid.NewGuid();

            var path = Path.Combine(Settings.Media.StoragePath, fileId.ToString());

            using (var http = new HttpClient())
            using (var fs = new FileStream(path, FileMode.Create))
            {
                var response = await http.GetStreamAsync(sourceUrl);

                await response.CopyToAsync(fs);
            }

            media.Crc32 = CalculateChecksum(path);
            media.FileId = fileId;

            return media;
        }

        public string CalculateChecksum(string path)
        {
            uint crc = 0;

            using (FileStream fs = File.Open(path, FileMode.Open))
            {
                var buffer = new byte[4096];

                while (true)
                {
                    var count = fs.Read(buffer, 0, buffer.Length);

                    if (count == 0)
                        break;

                    crc = Crc32Algorithm.Append(crc, buffer, 0, count);
                }
            }

            return crc.ToString("X");
        }
    }
}
