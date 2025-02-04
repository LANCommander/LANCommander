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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LANCommander.SDK.Exceptions;

namespace LANCommander.SDK.Services
{
    public class RedistributableService
    {
        private readonly ILogger Logger;
        private Client Client { get; set; }

        public delegate void OnArchiveEntryExtractionProgressHandler(object sender, ArchiveEntryExtractionProgressArgs e);
        public event OnArchiveEntryExtractionProgressHandler OnArchiveEntryExtractionProgress;

        public delegate void OnArchiveExtractionProgressHandler(long position, long length);
        public event OnArchiveExtractionProgressHandler OnArchiveExtractionProgress;
        
        public delegate void OnInstallProgressUpdateHandler(InstallProgress e);
        public event OnInstallProgressUpdateHandler OnInstallProgressUpdate;
        
        private InstallProgress _installProgress;

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
                await InstallAsync(redistributable, game);
            }
        }

        public async Task InstallAsync(Redistributable redistributable, Game game, int maxAttempts = 10)
        {
            string extractTempPath = null;
            
            _installProgress = new InstallProgress();
            
            _installProgress.Status = InstallStatus.Downloading;
            _installProgress.Title = redistributable.Name;
            _installProgress.Progress = 0;
            _installProgress.TransferSpeed = 0;
            _installProgress.TotalBytes = 0;
            _installProgress.BytesDownloaded = 0;
            
            OnInstallProgressUpdate?.Invoke(_installProgress);

            try
            {
                Logger?.LogTrace("Saving manifest");

                await ManifestHelper.WriteAsync(redistributable, game.InstallDirectory);
                
                Logger?.LogTrace("Saving scripts");
                    
                foreach (var script in redistributable.Scripts)
                {
                    await ScriptHelper.SaveScriptAsync(game, redistributable, script.Type);
                }
                
                var installed = await Client.Scripts.RunDetectInstallScriptAsync(game.InstallDirectory, game.Id, redistributable.Id);

                Logger?.LogTrace("Redistributable install detection returned {Result}", installed);

                if (!installed)
                {
                    Logger?.LogTrace("Redistributable {RedistributableName} not installed", redistributable.Name);
                    
                    if (redistributable.Archives.Any())
                    {
                        Logger?.LogTrace("Archives for redistributable {RedistributableName} exist. Attempting to download...", redistributable.Name);

                        var result = await RetryHelper.RetryOnExceptionAsync(maxAttempts,
                            TimeSpan.FromMilliseconds(500), new ExtractionResult(),
                            async () =>
                            {
                                Logger?.LogTrace("Attempting to download and extract redistributable");

                                return await Task.Run(() => DownloadAndExtract(redistributable, game));
                            });
                        
                        if (!result.Success && !result.Canceled)
                            throw new InstallException("Could not extract the redistributable. Retry the install or check your connection");
                        else if (result.Canceled)
                            throw new InstallCanceledException("Redistributable install canceled");

                        extractTempPath = result.Directory;
                        
                        Logger?.LogTrace("Extraction of redistributable successful. Extracted path is {Path}", extractTempPath);
                        Logger?.LogTrace("Running install script for redistributable {RedistributableName}", redistributable.Name);

                        await RunPostInstallScripts(game, redistributable);
                    }
                    else
                    {
                        Logger?.LogTrace("No archives exist for redistributable {RedistributableName}. Running install script anyway...", redistributable.Name);

                        await RunPostInstallScripts(game, redistributable);
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
        
        private async Task RunPostInstallScripts(Game game, Redistributable redistributable)
        {
            if (redistributable.Scripts != null && redistributable.Scripts.Any())
            {
                //GameInstallProgress.Status = GameInstallStatus.RunningScripts;

                // OnGameInstallProgressUpdate?.Invoke(GameInstallProgress);

                try
                {
                    await Client.Scripts.RunInstallScriptAsync(game.InstallDirectory, game.Id, redistributable.Id);
                    await Client.Scripts.RunNameChangeScriptAsync(game.InstallDirectory, game.Id, redistributable.Id, await Client.Profile.GetAliasAsync());
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "Scripts failed to execute for redistributable {RedistributableName} ({GameId})", redistributable.Name, redistributable.Id);
                }
            }
        }

        private ExtractionResult DownloadAndExtract(Redistributable redistributable, Game game)
        {
            if (redistributable == null)
            {
                Logger?.LogTrace("Redistributable failed to download! No redistributable was specified");
                throw new ArgumentNullException("No redistributable was specified");
            }

            var destination = Path.Combine(GameService.GetMetadataDirectoryPath(game.InstallDirectory, redistributable.Id), "Files");

            Logger?.LogTrace("Downloading and extracting {Redistributable} to path {Destination}", redistributable.Name, destination);

            try
            {
                Directory.CreateDirectory(destination);

                using (var redistributableStream = Stream(redistributable.Id))
                using (var reader = ReaderFactory.Open(redistributableStream))
                using (var monitor = new FileTransferMonitor(redistributableStream.Length))
                {
                    
                    redistributableStream.OnProgress += (pos, len) =>
                    {
                        if (monitor.CanUpdate())
                        {
                            monitor.Update(pos);

                            _installProgress.BytesDownloaded = monitor.GetBytesTransferred();
                            _installProgress.TotalBytes = len;
                            _installProgress.TransferSpeed = monitor.GetSpeed();
                            _installProgress.TimeRemaining = monitor.GetTimeRemaining();
                            
                            OnInstallProgressUpdate?.Invoke(_installProgress);
                        }
                    };

                    reader.EntryExtractionProgress += (sender, e) =>
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
