using Force.Crc32;
using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace LANCommander.SDK.Services
{
    public class MediaService
    {
        private readonly ILogger _logger;

        private readonly Client _client;

        public MediaService(Client client)
        {
            _client = client;
        }

        public MediaService(Client client, ILogger logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task<Media> Get(Guid mediaId)
        {
            return await _client.GetRequestAsync<Media>($"/api/Media/{mediaId}");
        }

        public async Task<string> DownloadAsync(Media media, string destination)
        {
            return await _client.DownloadRequestAsync(GetDownloadPath(media), destination);
        }

        public string GetAbsoluteUrl(Media media)
        {
            return new Uri(_client.BaseUrl, GetDownloadPath(media)).ToString();
        }

        public string GetDownloadPath(Media media)
        {
            return $"/api/Media/{media.Id}/Download?fileId={media.FileId}";
        }

        public string GetAbsoluteThumbnailUrl(Media media)
        {
            return new Uri(_client.BaseUrl, GetThumbnailPath(media)).ToString();
        }

        public string GetThumbnailPath(Media media)
        {
            return $"/api/Media/{media.Id}/Thumbnail";
        }

        public static string CalculateChecksum(string path)
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
