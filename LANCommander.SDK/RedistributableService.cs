using LANCommander.SDK.Enums;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;
using LANCommander.SDK.PowerShell;
using Microsoft.Extensions.Logging;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.SDK
{
    public class RedistributableService
    {
        private readonly ILogger Logger;
        private Client Client { get; set; }

        public delegate void OnArchiveEntryExtractionProgressHandler(object sender, ArchiveEntryExtractionProgressArgs e);
        public event OnArchiveEntryExtractionProgressHandler OnArchiveEntryExtractionProgress;

        public delegate void OnArchiveExtractionProgressHandler(long position, long length);
        public event OnArchiveExtractionProgressHandler OnArchiveExtractionProgress;

        public RedistributableService(Client client)
        {
            Client = client;
        }

        public RedistributableService(Client client, ILogger logger)
        {
            Client = client;
            Logger = logger;
        }

        public TrackableStream Stream(Guid id)
        {
            return Client.StreamRequest($"/api/Redistributables/{id}/Download");
        }

        public async Task InstallAsync(Game game)
        {
            foreach (var redistributable in game.Redistributables)
            {
                await InstallAsync(redistributable);
            }
        }

        public async Task InstallAsync(Redistributable redistributable)
        {
            string extractTempPath = null;

            try
            {
                var installed = await Client.Scripts.RunDetectInstallScriptAsync(redistributable);

                Logger?.LogTrace("Redistributable install detection returned {Result}", installed);

                if (!installed)
                {
                    Logger?.LogTrace("Redistributable {RedistributableName} not installed", redistributable.Name);

                    if (redistributable.Archives.Count() > 0)
                    {
                        Logger?.LogTrace("Archives for redistributable {RedistributableName} exist. Attempting to download...", redistributable.Name);

                        var extractionResult = DownloadAndExtract(redistributable);

                        if (extractionResult.Success)
                        {
                            extractTempPath = extractionResult.Directory;

                            Logger?.LogTrace("Extraction of redistributable successful. Extracted path is {Path}", extractTempPath);
                            Logger?.LogTrace("Running install script for redistributable {RedistributableName}", redistributable.Name);

                            await Client.Scripts.RunInstallScriptAsync(redistributable);
                        }
                        else
                        {
                            Logger?.LogError("There was an issue downloading and extracting the archive for redistributable {RedistributableName}", redistributable.Name);
                        }
                    }
                    else
                    {
                        Logger?.LogTrace("No archives exist for redistributable {RedistributableName}. Running install script anyway...", redistributable.Name);

                        await Client.Scripts.RunInstallScriptAsync(redistributable);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Redistributable {Redistributable} failed to install", redistributable.Name);
            }
            finally
            {
                if (Directory.Exists(extractTempPath))
                    Directory.Delete(extractTempPath, true);
            }
        }

        private ExtractionResult DownloadAndExtract(Redistributable redistributable)
        {
            if (redistributable == null)
            {
                Logger?.LogTrace("Redistributable failed to download! No redistributable was specified");
                throw new ArgumentNullException("No redistributable was specified");
            }

            var destination = Path.Combine(Path.GetTempPath(), redistributable.Name.SanitizeFilename());

            Logger?.LogTrace("Downloading and extracting {Redistributable} to path {Destination}", redistributable.Name, destination);

            try
            {
                Directory.CreateDirectory(destination);

                using (var redistributableStream = Stream(redistributable.Id))
                using (var reader = ReaderFactory.Open(redistributableStream))
                {
                    redistributableStream.OnProgress += (pos, len) =>
                    {
                        OnArchiveExtractionProgress?.Invoke(pos, len);
                    };

                    reader.EntryExtractionProgress += (object sender, ReaderExtractionEventArgs<IEntry> e) =>
                    {
                        OnArchiveEntryExtractionProgress?.Invoke(this, new ArchiveEntryExtractionProgressArgs
                        {
                            Entry = e.Item,
                            Progress = e.ReaderProgress,
                        });
                    };

                    reader.WriteAllToDirectory(destination, new ExtractionOptions()
                    {
                        ExtractFullPath = true,
                        Overwrite = true
                    });
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Could not extract to path {Destination}", destination);

                if (Directory.Exists(destination))
                {
                    Logger?.LogTrace("Cleaning up orphaned files after bad install");

                    Directory.Delete(destination, true);
                }

                throw new Exception("The redistributable archive could not be extracted, is it corrupted? Please try again");
            }

            var extractionResult = new ExtractionResult
            {
                Canceled = false
            };

            if (!extractionResult.Canceled)
            {
                extractionResult.Success = true;
                extractionResult.Directory = destination;
                Logger?.LogTrace("Redistributable {Redistributable} successfully downloaded and extracted to {Destination}", redistributable.Name, destination);
            }

            return extractionResult;
        }

        public async Task ImportAsync(string archivePath)
        {
            using (var fs = new FileStream(archivePath, FileMode.Open, FileAccess.Read))
            {
                var objectKey = await Client.ChunkedUploadRequestAsync("", fs);

                if (objectKey != Guid.Empty)
                    await Client.PostRequestAsync<object>($"/api/Redistributables/Import/{objectKey}");
            }
        }

        public async Task ExportAsync(string destinationPath, Guid redistributableId)
        {
            await Client.DownloadRequestAsync($"/Redistributables/{redistributableId}/Export/Full", destinationPath);
        }

        public async Task UploadArchiveAsync(string archivePath, Guid redistributableId, string version, string changelog = "")
        {
            using (var fs = new FileStream(archivePath, FileMode.Open, FileAccess.Read))
            {
                var objectKey = await Client.ChunkedUploadRequestAsync("", fs);

                if (objectKey != Guid.Empty)
                    await Client.PostRequestAsync<object>($"/api/Redistributables/UploadArchive", new UploadArchiveRequest
                    {
                        Id = redistributableId,
                        ObjectKey = objectKey,
                        Version = version,
                        Changelog = changelog,
                    });
            }
        }
    }
}
