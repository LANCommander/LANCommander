using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace LANCommander.SDK.Services
{
    public class ServerService
    {
        private readonly ILogger Logger;
        private Client Client { get; set; }

        public delegate void OnArchiveEntryExtractionProgressHandler(object sender, ArchiveEntryExtractionProgressArgs e);
        public event OnArchiveEntryExtractionProgressHandler OnArchiveEntryExtractionProgress;

        public delegate void OnArchiveExtractionProgressHandler(long position, long length);
        public event OnArchiveExtractionProgressHandler OnArchiveExtractionProgress;

        public ServerService(Client client)
        {
            Client = client;
        }

        public ServerService(Client client, ILogger logger)
        {
            Client = client;
            Logger = logger;
        }

        public async Task ImportAsync(string archivePath)
        {
            using (var fs = new FileStream(archivePath, FileMode.Open, FileAccess.Read))
            {
                var objectKey = await Client.ChunkedUploadRequestAsync("", fs);

                if (objectKey != Guid.Empty)
                    await Client.PostRequestAsync<object>($"/api/Servers/Import/{objectKey}");
            }
        }

        public async Task ExportAsync(string destinationPath, Guid serverId)
        {
            await Client.DownloadRequestAsync($"/Servers/{serverId}/Export/Full", destinationPath);
        }

        public async Task UploadArchiveAsync(string archivePath, Guid serverId, string version, string changelog = "")
        {
            using (var fs = new FileStream(archivePath, FileMode.Open, FileAccess.Read))
            {
                var objectKey = await Client.ChunkedUploadRequestAsync("", fs);

                if (objectKey != Guid.Empty)
                    await Client.PostRequestAsync<object>($"/api/Servers/UploadArchive", new UploadArchiveRequest
                    {
                        Id = serverId,
                        ObjectKey = objectKey,
                        Version = version,
                        Changelog = changelog,
                    });
            }
        }
    }
}
