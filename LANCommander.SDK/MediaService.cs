using LANCommander.SDK;
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

namespace LANCommander.SDK
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

        public async Task<string> Download(Media media, string destination)
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
    }
}
