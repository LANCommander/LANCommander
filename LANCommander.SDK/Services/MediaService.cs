using Force.Crc32;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;
using LANCommander.SDK.PowerShell;
using Microsoft.Extensions.Logging;
using RestSharp;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LANCommander.SDK.Services
{
    public class MediaService
    {
        private readonly ILogger Logger;

        private readonly Client Client;

        public MediaService(Client client)
        {
            Client = client;
        }

        public MediaService(Client client, ILogger logger)
        {
            Client = client;
            Logger = logger;
        }

        public async Task<Media> Get(Guid mediaId)
        {
            return await Client.GetRequestAsync<Media>($"/api/Media/{mediaId}");
        }

        public async Task<string> DownloadAsync(Media media, string destination)
        {
            return await Client.DownloadRequestAsync(GetDownloadPath(media), destination);
        }

        public string GetAbsoluteUrl(Media media)
        {
            return new Uri(Client.BaseUrl, GetDownloadPath(media)).ToString();
        }

        public string GetDownloadPath(Media media)
        {
            return $"/api/Media/{media.Id}/Download?fileId={media.FileId}";
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
