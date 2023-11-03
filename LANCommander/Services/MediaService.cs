using LANCommander.Data;
using LANCommander.Data.Models;
using LANCommander.Helpers;
using LANCommander.Models;

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

        public string GetImagePath(Media entity)
        {
            return Path.Combine(Settings.Media.StoragePath, entity.FileId.ToString());
        }

        public void DeleteLocalMediaFile(Guid fileId)
        {
            var path = Path.Combine(Settings.Media.StoragePath, fileId.ToString());

            if (File.Exists(path))
                File.Delete(path);
        }

        public async Task<Guid> DownloadMediaAsync(string sourceUrl)
        {
            var fileId = Guid.NewGuid();

            var path = Path.Combine(Settings.Media.StoragePath, fileId.ToString());

            using (var http = new HttpClient())
            using (var fs = new FileStream(path, FileMode.Create))
            {
                var response = await http.GetStreamAsync(sourceUrl);

                await response.CopyToAsync(fs);
            }

            return fileId;
        }
    }
}
