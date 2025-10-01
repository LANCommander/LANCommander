using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Exceptions;
using LANCommander.SDK.Factories;
using Microsoft.Extensions.Options;

namespace LANCommander.SDK.Services
{
    public class RedistributableClient(
        ILogger<RedistributableClient> _logger,
        IOptions<Settings> settings,
        ApiRequestFactory apiRequestFactory,
        ScriptClient scriptClient,
        ProfileClient profileClient)
    {
        public delegate void OnArchiveEntryExtractionProgressHandler(object sender, ArchiveEntryExtractionProgressArgs e);
        public event OnArchiveEntryExtractionProgressHandler OnArchiveEntryExtractionProgress;

        public delegate void OnArchiveExtractionProgressHandler(long position, long length);
        public event OnArchiveExtractionProgressHandler OnArchiveExtractionProgress;
        
        public delegate void OnInstallProgressUpdateHandler(InstallProgress e);
        public event OnInstallProgressUpdateHandler OnInstallProgressUpdate;
        
        private InstallProgress _installProgress;
        
        public async Task<TrackableStream> Stream(Guid id)
        {
            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute($"/api/Redistributable/{id}/Download")
                .StreamAsync();
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
            _installProgress.BytesTransferred = 0;
            
            OnInstallProgressUpdate?.Invoke(_installProgress);

            try
            {
                _logger?.LogTrace("Saving manifest");

                await ManifestHelper.WriteAsync(redistributable, game.InstallDirectory);
                
                _logger?.LogTrace("Saving scripts");
                    
                foreach (var script in redistributable.Scripts)
                {
                    await ScriptHelper.SaveScriptAsync(game, redistributable, script.Type);
                }

                var installed =
                    await scriptClient.RunDetectInstallScriptAsync(game.InstallDirectory, game.Id, redistributable.Id);

                _logger?.LogTrace("Redistributable install detection returned {Result}", installed);

                if (!installed)
                {
                    _logger?.LogTrace("Redistributable {RedistributableName} not installed", redistributable.Name);
                    
                    if (redistributable.Archives.Any())
                    {
                        _logger?.LogTrace("Archives for redistributable {RedistributableName} exist. Attempting to download...", redistributable.Name);

                        var result = await RetryHelper.RetryOnExceptionAsync(maxAttempts,
                            TimeSpan.FromMilliseconds(500), new ExtractionResult(),
                            async () =>
                            {
                                _logger?.LogTrace("Attempting to download and extract redistributable");

                                return await Task.Run(async () => await DownloadAndExtractAsync(redistributable, game));
                            });
                        
                        if (!result.Success && !result.Canceled)
                            throw new InstallException("Could not extract the redistributable. Retry the install or check your connection");
                        else if (result.Canceled)
                            throw new InstallCanceledException("Redistributable install canceled");

                        extractTempPath = result.Directory;
                        
                        _logger?.LogTrace("Extraction of redistributable successful. Extracted path is {Path}", extractTempPath);
                        _logger?.LogTrace("Running install script for redistributable {RedistributableName}", redistributable.Name);

                        await RunPostInstallScripts(game, redistributable);
                    }
                    else
                    {
                        _logger?.LogTrace("No archives exist for redistributable {RedistributableName}. Running install script anyway...", redistributable.Name);

                        await RunPostInstallScripts(game, redistributable);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Redistributable {Redistributable} failed to install", redistributable.Name);
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
                    await scriptClient.RunInstallScriptAsync(game.InstallDirectory, game.Id, redistributable.Id);
                    await scriptClient.RunNameChangeScriptAsync(game.InstallDirectory, game.Id, redistributable.Id, await profileClient.GetAliasAsync());
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Scripts failed to execute for redistributable {RedistributableName} ({GameId})", redistributable.Name, redistributable.Id);
                }
            }
        }

        private async Task<ExtractionResult> DownloadAndExtractAsync(Redistributable redistributable, Game game)
        {
            if (redistributable == null)
            {
                _logger?.LogTrace("Redistributable failed to download! No redistributable was specified");
                throw new ArgumentNullException(nameof(redistributable));
            }

            var destination = Path.Combine(GameClient.GetMetadataDirectoryPath(game.InstallDirectory, redistributable.Id), "Files");
            var files = new List<ExtractionResult.FileEntry>();

            _logger?.LogTrace("Downloading and extracting {Redistributable} to path {Destination}", redistributable.Name, destination);

            try
            {
                Directory.CreateDirectory(destination);

                using (var redistributableStream = await Stream(redistributable.Id))
                using (var reader = ReaderFactory.Open(redistributableStream))
                using (var monitor = new FileTransferMonitor(redistributableStream.Length))
                {
                    redistributableStream.OnProgress += (pos, len) =>
                    {
                        if (monitor.CanUpdate())
                        {
                            monitor.Update(pos);

                            _installProgress.BytesTransferred = monitor.GetBytesTransferred();
                            _installProgress.TotalBytes = len;
                            _installProgress.TransferSpeed = monitor.GetSpeed();
                            _installProgress.TimeRemaining = monitor.GetTimeRemaining();
                            
                            OnInstallProgressUpdate?.Invoke(_installProgress);
                        }
                    };

                    reader.EntryExtractionProgress += (sender, e) =>
                    {
                        files.Add(new ExtractionResult.FileEntry
                        {
                            EntryPath = e.Item.Key,
                            LocalPath = Path.Combine(destination, e.Item.Key),
                        });

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
                _logger?.LogError(ex, "Could not extract to path {Destination}", destination);

                if (Directory.Exists(destination))
                {
                    _logger?.LogTrace("Cleaning up orphaned files after bad install");

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
                extractionResult.Files = files;
                _logger?.LogTrace("Redistributable {Redistributable} successfully downloaded and extracted to {Destination}", redistributable.Name, destination);
            }

            return extractionResult;
        }

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
                        .UseRoute($"/api/Redistributables/Import/{objectKey}")
                        .PostAsync();
            }
        }

        [Obsolete("Exporter no longer provides \"full\" exports")]
        public async Task ExportAsync(string destinationPath, Guid redistributableId)
        {
            await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute($"/Redistributables/{redistributableId}/Export/Full")
                .DownloadAsync(destinationPath);
        }

        public async Task UploadArchiveAsync(string archivePath, Guid redistributableId, string version, string changelog = "")
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
                        .UseRoute("/api/Redistributables/UploadArchive")
                        .AddBody(new UploadArchiveRequest
                        {
                            Id = redistributableId,
                            ObjectKey = objectKey,
                            Version = version,
                            Changelog = changelog,
                        })
                        .PostAsync();
            }
        }
    }
}
