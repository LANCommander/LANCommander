using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Factories;

namespace LANCommander.SDK.Services
{
    public class ServerClient(
        ILANCommanderConfiguration config,
        ApiRequestFactory apiRequestFactory)
    {
        public delegate void OnArchiveEntryExtractionProgressHandler(object sender, ArchiveEntryExtractionProgressArgs e);
        public event OnArchiveEntryExtractionProgressHandler OnArchiveEntryExtractionProgress;

        public delegate void OnArchiveExtractionProgressHandler(long position, long length);
        public event OnArchiveExtractionProgressHandler OnArchiveExtractionProgress;

        public async Task ImportAsync(string archivePath)
        {
            using (var fs = new FileStream(archivePath, FileMode.Open, FileAccess.Read))
            {
                var objectKey = await apiRequestFactory
                    .Create()
                    .UseAuthenticationToken()
                    .UseVersioning()
                    .UploadInChunksAsync(config.UploadChunkSize, fs);

                if (objectKey != Guid.Empty)
                    await apiRequestFactory
                        .Create()
                        .UseAuthenticationToken()
                        .UseVersioning()
                        .UseRoute($"/api/Servers/Import/{objectKey}")
                        .PostAsync<object>();
            }
        }

        [Obsolete]
        public async Task ExportAsync(string destinationPath, Guid serverId)
        {
            throw new NotImplementedException();
        }

        public async Task UploadArchiveAsync(string archivePath, Guid serverId, string version, string changelog = "")
        {
            using (var fs = new FileStream(archivePath, FileMode.Open, FileAccess.Read))
            {
                var objectKey = await apiRequestFactory
                    .Create()
                    .UseAuthenticationToken()
                    .UseVersioning()
                    .UploadInChunksAsync(config.UploadChunkSize, fs);
                
                if (objectKey != Guid.Empty)
                    await apiRequestFactory
                        .Create()
                        .UseAuthenticationToken()
                        .UseVersioning()
                        .UseRoute($"/api/Servers/UploadArchive")
                        .AddBody(new UploadArchiveRequest
                        {
                            Id = serverId,
                            ObjectKey = objectKey,
                            Version = version,
                            Changelog = changelog,
                        })
                        .PostAsync<object>();
            }
        }
    }
}
