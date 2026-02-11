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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Force.Crc32;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Exceptions;
using LANCommander.SDK.Factories;

namespace LANCommander.SDK.Services
{
    public class ToolClient(
        ILogger<ToolClient> logger,
        ISettingsProvider settingsProvider,
        ApiRequestFactory apiRequestFactory,
        ScriptClient scriptClient,
        ProfileClient profileClient)
    {
        public delegate void OnArchiveEntryExtractionProgressHandler(object sender, ArchiveEntryExtractionProgressArgs e);
        public event OnArchiveEntryExtractionProgressHandler OnArchiveEntryExtractionProgress;

        public delegate void OnArchiveExtractionProgressHandler(long position, long length, Tool tool);
        public event OnArchiveExtractionProgressHandler OnArchiveExtractionProgress;
        
        public delegate void OnInstallProgressUpdateHandler(InstallProgress e);
        public event OnInstallProgressUpdateHandler OnInstallProgressUpdate;

        private TrackableStream _transferStream;
        private IReader _reader;
        
        private InstallProgress _installProgress;
        
        public async Task<Tool> GetAsync(Guid id)
        {
            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute($"/api/Tools/{id}")
                .GetAsync<Tool>();
        }

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
                await InstallAsync(tool, game.InstallDirectory);
            }
        }

        public async Task<InstallResult> InstallAsync(Tool tool, string installDirectory, int maxAttempts = 10)
        {
            string extractTempPath = null;

            var installResult = new InstallResult();
            
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
                logger?.LogTrace("Saving manifest");
                
                var manifest = await GetManifestAsync(tool.Id);

                await ManifestHelper.WriteAsync(manifest, installDirectory);
                
                logger?.LogTrace("Saving scripts");
                    
                foreach (var script in tool.Scripts)
                    await ScriptHelper.SaveScriptAsync(tool, script.Type, installDirectory);
                
                if (tool.Archives?.Any() ?? false)
                {
                    logger?.LogTrace("Archives for tool {ToolName} exist. Attempting to download...", tool.Name);

                    var result = await RetryHelper.RetryOnExceptionAsync(maxAttempts,
                        TimeSpan.FromMilliseconds(500), new ExtractionResult(),
                        async () =>
                        {
                            logger?.LogTrace("Attempting to download and extract tool");

                            return await Task.Run(async () => await DownloadAndExtractAsync(tool, installDirectory));
                        });
                        
                    if (!result.Success && !result.Canceled)
                        throw new InstallException("Could not extract the tool. Retry the install or check your connection");
                    else if (result.Canceled)
                        throw new InstallCanceledException("Tool install canceled");

                    extractTempPath = result.Directory;
                        
                    logger?.LogTrace("Extraction of tool successful. Extracted path is {Path}", extractTempPath);
                    logger?.LogTrace("Running install script for tool {ToolName}", tool.Name);

                    await RunPostInstallScripts(installDirectory, tool);
                }
                
                if (tool.Archives?.Any() ?? false)
                {
                    logger?.LogTrace("Archives for tool {ToolName} exist. Attempting to download...", tool.Name);

                    var result = await RetryHelper.RetryOnExceptionAsync(maxAttempts,
                        TimeSpan.FromMilliseconds(500), new ExtractionResult(),
                        async () =>
                        {
                            logger?.LogTrace("Attempting to download and extract tool");

                            return await Task.Run(async () => await DownloadAndExtractAsync(tool, installDirectory));
                        });
                    
                    if (!result.Success && !result.Canceled)
                        throw new InstallException("Could not extract the tool. Retry the install or check your connection");
                    else if (result.Canceled)
                        throw new InstallCanceledException("Tool install canceled");

                    extractTempPath = result.Directory;

                    installResult.InstallDirectory = result.Directory;

                    // TODO: Verification for tool files?
                    var toolFiles = result?.Files?.Where(x => !x.EntryPath.EndsWith("/")).Select(x =>
                        new GameInstallationFileListEntry.FileEntry
                        {
                            EntryPath = x.EntryPath,
                            LocalPath = x.LocalPath,
                        });
                    
                    logger?.LogTrace("Extraction of tool successful. Extracted path is {Path}", extractTempPath);
                    logger?.LogTrace("Running install script for tool {ToolName}", tool.Name);

                    await RunPostInstallScripts(installDirectory, tool);
                }
                else
                {
                    logger?.LogTrace("No archives exist for tool {ToolName}. Running install script anyway...", tool.Name);

                    await RunPostInstallScripts(installDirectory, tool);
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Tool {Tool} failed to install", tool.Name);
            }
            finally
            {
                if (Directory.Exists(extractTempPath))
                    Directory.Delete(extractTempPath, true);
            }

            return installResult;
        }
        
        private async Task RunPostInstallScripts(string installDirectory, Tool tool)
        {
            if (tool.Scripts != null && tool.Scripts.Any())
            {
                //GameInstallProgress.Status = GameInstallStatus.RunningScripts;

                // OnGameInstallProgressUpdate?.Invoke(GameInstallProgress);

                try
                {
                    await scriptClient.Tool_RunInstallScriptAsync(installDirectory, tool.Id);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Scripts failed to execute for tool {ToolName} ({GameId})", tool.Name, tool.Id);
                }
            }
        }

        private async Task<ExtractionResult> DownloadAndExtractAsync(Tool tool, string destination, CancellationToken cancellationToken = default)
        {
            if (tool == null)
            {
                logger?.LogTrace("Tool failed to download! No tool was specified");
                throw new ArgumentNullException(nameof(tool));
            }

            logger?.LogTrace("Downloading and extracting {Tool} to path {Destination}", tool.Name, destination);

            var extractionResult = new ExtractionResult
            {
                Canceled = false,
            };

            if (!await CanStreamLatestArchiveAsync(tool.Id))
            {
                extractionResult.Success = false;
                extractionResult.Canceled = true;

                return extractionResult;
            }

            var fileManifest = new StringBuilder();
            var files = new List<ExtractionResult.FileEntry>();

            try
            {
                Directory.CreateDirectory(destination);

                var stream = await StreamLatestArchiveAsync(tool.Id);

                _reader = ReaderFactory.Open(stream);

                using (var monitor = new FileTransferMonitor(stream.Length))
                {
                    _reader.EntryExtractionProgress += (sender, e) =>
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            _reader.Cancel();

                            _installProgress.Status = InstallStatus.Canceled;

                            OnInstallProgressUpdate?.Invoke(_installProgress);

                            return;
                        }

                        if (monitor.CanUpdate())
                        {
                            monitor.Update(stream.Position);

                            _installProgress.BytesTransferred = monitor.GetBytesTransferred();
                            _installProgress.TotalBytes = stream.Length;
                            _installProgress.TransferSpeed = monitor.GetSpeed();
                            _installProgress.TimeRemaining = monitor.GetTimeRemaining();

                            OnInstallProgressUpdate?.Invoke(_installProgress);
                        }

                        OnArchiveEntryExtractionProgress?.Invoke(this, new ArchiveEntryExtractionProgressArgs
                        {
                            Entry = e.Item,
                            Progress = e.ReaderProgress,
                            Game = null,
                        });
                    };
                }

                while (_reader.MoveToNextEntry())
                {
                    if (_reader.Cancelled)
                        break;

                    try
                    {
                        var localFile = Path.Combine(destination, _reader.Entry.Key);

                        uint crc = 0;

                        if (File.Exists(localFile))
                        {
                            using (FileStream fs = File.Open(localFile, FileMode.Open))
                            {
                                var buffer = new byte[65536];

                                while (true)
                                {
                                    var count = fs.Read(buffer, 0, buffer.Length);

                                    if (count == 0)
                                        break;

                                    crc = Crc32Algorithm.Append(crc, buffer, 0, count);
                                }
                            }
                        }

                        fileManifest.AppendLine($"{_reader.Entry.Key} | {_reader.Entry.Crc.ToString("X")}");
                        files.Add(new ExtractionResult.FileEntry
                        {
                            EntryPath = _reader.Entry.Key,
                            LocalPath = localFile,
                        });

                        if (crc == 0 || crc != _reader.Entry.Crc)
                            _reader.WriteEntryToDirectory(destination, new ExtractionOptions()
                            {
                                ExtractFullPath = true,
                                Overwrite = true,
                                PreserveFileTime = true
                            });
                        else // Skip to next entry
                            try
                            {
                                _reader.OpenEntryStream().Dispose();
                            }
                            catch
                            {
                                logger?.LogError("Could not skip to next entry in archive");
                            }
                    }
                    catch (IOException ex)
                    {
                        var errorCode = ex.HResult & 0xFFFF;

                        if (errorCode == 87)
                            throw ex;
                        else
                            logger?.LogTrace("Not replacing existing file/folder on disk: {Message}", ex.Message);

                        // Skip to next entry
                        _reader.OpenEntryStream().Dispose();
                    }
                }

                _reader.Dispose();
                await stream.DisposeAsync();
            }
            catch (ReaderCancelledException ex)
            {
                logger?.LogTrace(ex, "User cancelled the download");

                extractionResult.Canceled = true;

                if (Directory.Exists(destination))
                {
                    logger?.LogTrace("Cleaning up ophaned files after cancelled install");

                    Directory.Delete(destination, true);
                }
            }
            catch (Exception ex)
            {                
                logger?.LogError(ex, "Could not extract to path {Destination}", destination);

                if (Directory.Exists(destination))
                {
                    logger?.LogTrace("Cleaning up orphaned install files after bad install");

                    Directory.Delete(destination, true);
                }

                throw new Exception("The game archive could not be extracted, is it corrupted? Please try again");
            }

            if (!extractionResult.Canceled)
            {
                extractionResult.Success = true;
                extractionResult.Directory = destination;
                extractionResult.Files = files;

                var fileListDestination = Path.Combine(destination, ".lancommander", tool.Id.ToString(), "FileList.txt");

                if (!Directory.Exists(Path.GetDirectoryName(fileListDestination)))
                    Directory.CreateDirectory(Path.GetDirectoryName(fileListDestination));

                File.WriteAllText(fileListDestination, fileManifest.ToString());

                logger?.LogTrace("Tool {Tool} successfully downloaded and extracted to {Destination}", tool.Name, destination);
            }

            return extractionResult;
        }

        private async Task<bool> CanStreamLatestArchiveAsync(Guid id)
        {
            try
            {
                await apiRequestFactory
                    .Create()
                    .UseAuthenticationToken()
                    .UseVersioning()
                    .UseRoute($"/api/Tools/{id}/Download")
                    .HeadAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<TrackableStream> StreamLatestArchiveAsync(Guid id)
        {
            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute($"/api/Tools/{id}/Download")
                .StreamAsync();
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
