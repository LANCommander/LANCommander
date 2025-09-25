using Force.Crc32;
using LANCommander.SDK.Models;
using System;
using System.IO;
using System.Threading.Tasks;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Factories;

namespace LANCommander.SDK.Services
{
    public class MediaService(
        ApiRequestFactory apiRequestFactory,
        IConnectionClient connectionClient)
    {
        public async Task<Media> GetAsync(Guid mediaId)
        {
            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute($"/api/Media/{mediaId}")
                .GetAsync<Media>();
        }

        public async Task<FileInfo> DownloadAsync(Media media, string destination)
        {
            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute(GetDownloadPath(media))
                .DownloadAsync(destination);
        }

        public string GetAbsoluteUrl(Media media)
        {
            return connectionClient.GetServerAddress().Join(GetDownloadPath(media)).ToString();
        }

        public string GetDownloadPath(Media media)
        {
            return $"/api/Media/{media.Id}/Download?fileId={media.FileId}";
        }

        public string GetAbsoluteThumbnailUrl(Media media)
        {
            return connectionClient.GetServerAddress().Join(GetThumbnailPath(media)).ToString();
        }

        public string GetThumbnailPath(Media media)
        {
            return $"/api/Media/{media.Id}/Thumbnail";
        }

        public static async Task<string> CalculateChecksumAsync(string path)
        {
            uint crc = 0;

            using (FileStream fs = File.Open(path, FileMode.Open))
            {
                var buffer = new byte[4096];

                while (true)
                {
                    var count = await fs.ReadAsync(buffer, 0, buffer.Length);

                    if (count == 0)
                        break;

                    crc = Crc32Algorithm.Append(crc, buffer, 0, count);
                }
            }

            return crc.ToString("X");
        }
    }
}
