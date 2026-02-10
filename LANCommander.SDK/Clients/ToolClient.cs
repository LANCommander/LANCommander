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

namespace LANCommander.SDK.Services
{
    public class ToolClient(
        ILogger<ToolClient> _logger,
        ISettingsProvider settingsProvider,
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

        public async Task<SDK.Models.Manifest.Tool> GetManifestAsync(Guid id)
        {
            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute($"/api/Tool/{id}")
                .GetAsync<SDK.Models.Manifest.Tool>();
        }
        
        public async Task<Stream> Stream(Guid id)
        {
            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute($"/api/Tool/{id}/Download")
                .StreamAsync();
        }

        public async Task InstallAsync(Game game)
        {
            foreach (var tool in game.Tools)
            {
                await InstallAsync(tool, game);
            }
        }

        public async Task InstallAsync(Tool tool, Game game, int maxAttempts = 10)
        {
            string extractTempPath = null;
            
            _installProgress = new InstallProgress();
            
            _installProgress.Status = InstallStatus.Downloading;
            _installProgress.Title = tool.Name;
            _installProgress.Progress = 0;
            _installProgress.TransferSpeed = 0;
            _installProgress.TotalBytes = 0;
            _installProgress.BytesTransferred = 0;
            
            OnInstallProgressUpdate?.Invoke(_installProgress);

            try
            {
                _logger?.LogTrace("Saving manifest");
                
                var manifest = await GetManifestAsync(tool.Id);

                await ManifestHelper.WriteAsync(manifest, game.InstallDirectory);
                
                _logger?.LogTrace("Saving scripts");
                    
                foreach (var script in tool.Scripts)
                {
                    await ScriptHelper.SaveScriptAsync(game, tool, script.Type);
                }

                var installed =
                    await scriptClient.RunDetectInstallScriptAsync(game.InstallDirectory, game.Id, tool.Id);

                _logger?.LogTrace("Tool install detection returned {Result}", installed);

                if (!installed)
                {
                    _logger?.LogTrace("Tool {ToolName} not installed", tool.Name);
                    
                    if (tool.Archives?.Any() ?? false)
                    {
                        _logger?.LogTrace("Archives for tool {ToolName} exist. Attempting to download...", tool.Name);

                        var result = await RetryHelper.RetryOnExceptionAsync(maxAttempts,
                            TimeSpan.FromMilliseconds(500), new ExtractionResult(),
                            async () =>
                            {
                                _logger?.LogTrace("Attempting to download and extract tool");

                                return await Task.Run(async () => await DownloadAndExtractAsync(tool, game));
                            });
                        
                        if (!result.Success && !result.Canceled)
                            throw new InstallException("Could not extract the tool. Retry the install or check your connection");
                        else if (result.Canceled)
                            throw new InstallCanceledException("Tool install canceled");

                        extractTempPath = result.Directory;
                        
                        _logger?.LogTrace("Extraction of tool successful. Extracted path is {Path}", extractTempPath);
                        _logger?.LogTrace("Running install script for tool {ToolName}", tool.Name);

                        await RunPostInstallScripts(game, tool);
                    }
                    else
                    {
                        _logger?.LogTrace("No archives exist for tool {ToolName}. Running install script anyway...", tool.Name);

                        await RunPostInstallScripts(game, tool);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Tool {Tool} failed to install", tool.Name);
            }
            finally
            {
                if (Directory.Exists(extractTempPath))
                    Directory.Delete(extractTempPath, true);
            }
        }
        
        private async Task RunPostInstallScripts(Game game, Tool tool)
        {
            if (tool.Scripts != null && tool.Scripts.Any())
            {
                //GameInstallProgress.Status = GameInstallStatus.RunningScripts;

                // OnGameInstallProgressUpdate?.Invoke(GameInstallProgress);

                try
                {
                    await scriptClient.Redistributable_RunInstallScriptAsync(installDirectory, game.Id, tool.Id);
                    await scriptClient.Redistributable_RunNameChangeScriptAsync(game.InstallDirectory, game.Id, tool.Id, await profileClient.GetAliasAsync());
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Scripts failed to execute for tool {ToolName} ({GameId})", tool.Name, tool.Id);
                }
            }
        }

        private async Task<ExtractionResult> DownloadAndExtractAsync(Tool tool, Game game)
        {
            if (tool == null)
            {
                _logger?.LogTrace("Tool failed to download! No tool was specified");
                throw new ArgumentNullException(nameof(tool));
            }

            var destination = Path.Combine(GameClient.GetMetadataDirectoryPath(game.InstallDirectory, tool.Id), "Files");
            var files = new List<ExtractionResult.FileEntry>();

            _logger?.LogTrace("Downloading and extracting {Tool} to path {Destination}", tool.Name, destination);

            try
            {
                Directory.CreateDirectory(destination);

                using (var toolStream = await Stream(tool.Id))
                using (var reader = ReaderFactory.Open(toolStream))
                using (var monitor = new FileTransferMonitor(toolStream.Length))
                {
                    /*(toolStream.OnProgress += (pos, len) =>
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
                    };*/

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

                throw new Exception("The tool archive could not be extracted, is it corrupted? Please try again");
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
                _logger?.LogTrace("Tool {Tool} successfully downloaded and extracted to {Destination}", tool.Name, destination);
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
                    .UploadInChunksAsync(settingsProvider.CurrentValue.Archives.UploadChunkSize, fs);

                if (objectKey != Guid.Empty)
                    await apiRequestFactory
                        .Create()
                        .UseAuthenticationToken()
                        .UseVersioning()
                        .UseRoute($"/api/Tools/Import/{objectKey}")
                        .PostAsync();
            }
        }

        [Obsolete("Exporter no longer provides \"full\" exports")]
        public async Task ExportAsync(string destinationPath, Guid toolId)
        {
            await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute($"/Tools/{toolId}/Export/Full")
                .DownloadAsync(destinationPath);
        }

        public async Task UploadArchiveAsync(string archivePath, Guid toolId, string version, string changelog = "")
        {
            using (var fs = new FileStream(archivePath, FileMode.Open, FileAccess.Read))
            {
                var objectKey = await apiRequestFactory
                    .Create()
                    .UseAuthenticationToken()
                    .UseVersioning()
                    .UploadInChunksAsync(settingsProvider.CurrentValue.Archives.UploadChunkSize, fs);

                if (objectKey != Guid.Empty)
                    await apiRequestFactory
                        .Create()
                        .UseAuthenticationToken()
                        .UseVersioning()
                        .UseRoute("/api/Tools/UploadArchive")
                        .AddBody(new UploadArchiveRequest
                        {
                            Id = toolId,
                            ObjectKey = objectKey,
                            Version = version,
                            Changelog = changelog,
                        })
                        .PostAsync();
            }
        }
    }
}
