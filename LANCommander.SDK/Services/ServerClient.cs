using LANCommander.SDK.Models;
using System;
using System.IO;
using System.Threading.Tasks;
using LANCommander.SDK.Factories;
using Microsoft.Extensions.Options;

namespace LANCommander.SDK.Services
{
    public class ServerClient(
        IOptions<Settings> settings,
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
                    .UploadInChunksAsync(settings.Value.Archives.UploadChunkSize, fs);

                if (objectKey != Guid.Empty)
                    await apiRequestFactory
                        .Create()
                        .UseAuthenticationToken()
                        .UseVersioning()
                        .UseRoute($"/api/Servers/Import/{objectKey}")
                        .PostAsync();
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
                    .UploadInChunksAsync(settings.Value.Archives.UploadChunkSize, fs);
                
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
                        .PostAsync();
            }
        }
    }
}
