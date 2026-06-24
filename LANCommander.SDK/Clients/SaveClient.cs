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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Factories;
using LANCommander.SDK.Utilities;
using Action = System.Action;

// Some terms for this file since they're probably going to be needed in the future:
// Local path - The full path of the file/directory on the local disk. No variables used, just the raw path for current machine
// Actual path - The path where the entries should be extracted to, before expanding environemnt variables.
// Archive path - The path of where the entries are located in the ZIP
//
// Other notes:
// - Entries in the ZIP are separated by save path ID to avoid collision

namespace LANCommander.SDK.Services
{
    public class SaveClient(
        ApiRequestFactory apiRequestFactory,
        ISettingsProvider settingsProvider,
        PowerShellScriptFactory powerShellScriptFactory,
        ILogger<SaveClient> logger)
    {
        public delegate void OnDownloadProgressHandler(DownloadProgressChangedEventArgs e);
        public event OnDownloadProgressHandler OnDownloadProgress;

        public delegate void OnDownloadCompleteHandler();
        public event OnDownloadCompleteHandler OnDownloadComplete;

        private async Task<FileInfo> DownloadAsync(Guid id, Action<DownloadProgressChangedEventArgs> progressHandler, Action completeHandler)
        {
            var destination = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            
            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute($"/api/Saves/{id}/Download")
                .OnProgress(progressHandler)
                .OnComplete(completeHandler)
                .DownloadAsync(destination);
        }

        public async Task<FileInfo> DownloadLatestAsync(Guid gameId, Action<DownloadProgressChangedEventArgs> progressHandler, Action completeHandler)
        {
            var destination = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            
            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute($"/api/Saves/Game/{gameId}/Latest/Download")
                .OnProgress(progressHandler)
                .OnComplete(completeHandler)
                .DownloadAsync(destination); 
        }

        public async Task<IEnumerable<GameSave>> GetAsync(Guid gameId)
        {
            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute($"/api/Saves/Game/{gameId}")
                .GetAsync<IEnumerable<GameSave>>();
        }

        public async Task<GameSave> GetLatestAsync(Guid gameId)
        {
            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute($"/api/Saves/Game/{gameId}/Latest")
                .GetAsync<GameSave>();
        }

        public async Task DownloadAsync(string installDirectory, Guid gameId, Guid? saveId = null)
        {
            var manifest = await ManifestHelper.ReadAsync<SDK.Models.Manifest.Game>(installDirectory, gameId);

            string tempFile;
            string tempLocation = string.Empty;

            if (manifest != null)
            {
                FileInfo destination;

                if (!saveId.HasValue)
                {
                    destination = await DownloadLatestAsync(manifest.Id, (changed) =>
                    {
                        OnDownloadProgress?.Invoke(changed);
                    }, () =>
                    {
                        OnDownloadComplete?.Invoke();
                    });
                }
                else
                {
                    destination = await DownloadAsync(saveId.Value, (changed) =>
                    {
                        OnDownloadProgress?.Invoke(changed);
                    }, () =>
                    {
                        OnDownloadComplete?.Invoke();
                    });
                }
                
                if (!destination.Exists)
                {
                    logger?.LogWarning("Save archive was not downloaded for game {GameId}", gameId);
                    return;
                }

                logger?.LogDebug("Save archive downloaded to {SaveTempLocation} for game {GameId}", destination, gameId);

                tempFile = destination.FullName;

                // Go into the archive and extract the files to the correct locations
                try
                {
                    tempLocation = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

                    Directory.CreateDirectory(tempLocation);

                    bool success = RetryHelper.RetryOnException(10, TimeSpan.FromMilliseconds(200), false, () =>
                    {
                        logger?.LogTrace("Extracting save archive to temporary location {TempPath}", tempLocation);

                        ExtractFilesFromZip(tempFile, tempLocation);

                        return true;
                    });

                    if (!success)
                        throw new ExtractionException("Could not extract the save archive. Is the file locked?");

                    manifest = await ManifestHelper.ReadAsync<SDK.Models.Manifest.Game>(tempLocation);

                    #region Move files
                    var tempLocationFilePath = "Files";

                    // Legacy support
                    if (!Directory.Exists(Path.Combine(tempLocation, tempLocationFilePath)))
                        tempLocationFilePath = "Saves";

                    foreach (var savePath in manifest.SavePaths.Where(sp => sp.Type == Enums.SavePathType.File))
                    {
                        var entries = GetFileSavePathEntries(savePath, installDirectory) ?? [];

                        logger?.LogTrace("Processing save path {SavePathId} with {EntryCount} entries", savePath.Id, entries.Count());

                        foreach (var entry in entries)
                        {
                            var entryPath = Path.Combine(tempLocation, tempLocationFilePath, savePath.Id.ToString(), entry.ArchivePath.Replace('/', Path.DirectorySeparatorChar));
                            var destinationPath = entry.ActualPath.ExpandEnvironmentVariables(installDirectory);

                            if (File.Exists(entryPath))
                            {
                                var destinationDirectory = Path.GetDirectoryName(destinationPath);

                                Directory.CreateDirectory(destinationDirectory);

                                // Handle individual files that were saved as an entry in the path
                                if (File.Exists(destinationPath))
                                    File.Delete(destinationPath);

                                File.Move(entryPath, destinationPath);

                                logger?.LogTrace("Restored save file {ArchivePath} to {DestinationPath}", entry.ArchivePath, destinationPath);
                            }
                            else if (Directory.Exists(entryPath))
                            {
                                // Handle directories that were saved as an entry in the path
                                var entryFiles = Directory.GetFiles(entryPath, "*", SearchOption.AllDirectories);

                                foreach (var entryFile in entryFiles)
                                {
                                    var fileDestination = entryFile.Replace(entryPath, destinationPath);

                                    var destinationDirectory = Path.GetDirectoryName(fileDestination);

                                    Directory.CreateDirectory(destinationDirectory);

                                    if (File.Exists(fileDestination))
                                        File.Delete(fileDestination);

                                    File.Move(entryFile, fileDestination);
                                }

                                logger?.LogTrace("Restored save directory {ArchivePath} ({FileCount} files) to {DestinationPath}", entry.ArchivePath, entryFiles.Length, destinationPath);
                            }
                            else
                            {
                                logger?.LogWarning("Save entry {ArchivePath} not found in archive at {EntryPath}", entry.ArchivePath, entryPath);
                            }
                        }
                    }
                    #endregion

                    #region Handle registry importing
                    var registryImportFilePaths = Directory.GetFiles(tempLocation, "_registry*.reg");
                    var importer = new RegistryImportUtility();

                    foreach (var registryImportFilePath in registryImportFilePaths)
                    {
                        var registryImportFileContents = File.ReadAllText(registryImportFilePath);

                        var script = powerShellScriptFactory.Create(Enums.ScriptType.SaveDownload);

                        string adminArgument = string.Empty;
                        if (registryImportFileContents.Contains("HKEY_LOCAL_MACHINE"))
                        {
                            script.AsAdmin();
                            adminArgument = " -Verb RunAs";
                        }

                        script.UseInline($"Start-Process regedit.exe {adminArgument} -ArgumentList \"/s\", \"{registryImportFilePath}\"");

                        if (settingsProvider.CurrentValue.Debug.EnableScriptDebugging)
                        {
                            script.EnableDebug();
                        }

                        await script.ExecuteAsync<int>();
                    }
                    #endregion

                    // Clean up temp files
                    Directory.Delete(tempLocation, true);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Failed to extract save files for game {GameId}", gameId);
                }
                finally
                {
                    if (Directory.Exists(tempLocation))
                        Directory.Delete(tempLocation, true);
                }
            }
        }

        public async Task<Stream> PackAsync(string installDirectory, SDK.Models.Manifest.Game manifest)
        {
            using (var savePacker = new SavePacker(installDirectory))
            {
                if (manifest?.SavePaths.Any() ?? false)
                    savePacker.AddPaths(manifest.SavePaths);

                await savePacker.AddManifestAsync(manifest);
                
                return await savePacker.PackAsync();
            }
        }

        public async Task<GameSave> UploadAsync(Stream stream, SDK.Models.Manifest.Game manifest)
        {
            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute($"/api/Saves/Game/{manifest.Id}/Upload")
                .UploadAsync<GameSave>($"game-{manifest.Id}-save", stream);
        }

        public async Task UploadAsync(string installDirectory, Guid gameId)
        {
            using (var savePacker = new SavePacker(installDirectory))
            {
                var manifest = await ManifestHelper.ReadAsync<SDK.Models.Manifest.Game>(installDirectory, gameId);

                if (manifest?.SavePaths?.Any() ?? false)
                    savePacker.AddPaths(manifest.SavePaths);

                if (savePacker.HasEntries())
                {
                    logger?.LogDebug("Packing {EntryCount} save entries for game {GameId}", savePacker.EntryCount, gameId);

                    await savePacker.AddManifestAsync(manifest);

                    var stream = await savePacker.PackAsync();

                    await UploadAsync(stream, manifest);

                    logger?.LogDebug("Save uploaded for game {GameId} ({Size} bytes)", gameId, stream.Length);
                }
                else
                {
                    logger?.LogDebug("No save files found to upload for game {GameId}", gameId);
                }
            }
        }

        public async Task DeleteAsync(Guid id)
        {
            await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute($"/api/Saves/{id}")
                .DeleteAsync<bool>();
        }

        public IEnumerable<SavePathEntry> GetFileSavePathEntries(SDK.Models.Manifest.SavePath savePath, string installDirectory)
        {
            IEnumerable<string> localPaths;

            if (savePath.IsRegex)
            {
                var workingDirectory = GetLocalPath(savePath.WorkingDirectory, installDirectory);
                var pattern = savePath.Path;

                if (string.IsNullOrWhiteSpace(workingDirectory))
                    workingDirectory = installDirectory;

                var regex = new Regex(pattern);

                localPaths = Directory.GetFiles(workingDirectory, "*", SearchOption.AllDirectories)
                    .Where(p =>
                    {
                        var relativePath = p.Substring(workingDirectory.Length)
                            .TrimStart(Path.DirectorySeparatorChar)
                            .Replace('\\', '/');

                        return regex.IsMatch(relativePath);
                    })
                    .ToList();
            }
            else
            {
                var workingDirectory = GetLocalPath(savePath.WorkingDirectory, installDirectory);

                var localPath = Path.Combine(workingDirectory, GetLocalPath(savePath.Path, installDirectory));

                localPaths = new[] { localPath };
            }

            var entries = new List<SavePathEntry>();

            foreach (var localPath in localPaths)
            {
                var actualPath = localPath.DeflateEnvironmentVariables(installDirectory);
                var workingDirectory = savePath.WorkingDirectory.DeflateEnvironmentVariables(installDirectory);
                var archivePath = actualPath.Replace(workingDirectory, "").TrimStart(Path.DirectorySeparatorChar);

                entries.Add(new SavePathEntry
                {
                    ArchivePath = archivePath.Replace(Path.DirectorySeparatorChar, '/'),
                    ActualPath = actualPath.Replace(Path.DirectorySeparatorChar, '/')
                });

                savePath.Entries = entries;
            }

            return entries;
        }

        public string GetLocalPath(string path, string installDirectory)
        {
            var localPath = path.ExpandEnvironmentVariables(installDirectory);

            if (Path.DirectorySeparatorChar == '/')
                localPath = localPath.Replace('\\', Path.DirectorySeparatorChar);
            else
                localPath = localPath.Replace('/', Path.DirectorySeparatorChar);

            return localPath;
        }

        public string GetActualPath(string path, string installDirectory)
        {
            var actualPath = path.DeflateEnvironmentVariables(installDirectory);

            if (Path.DirectorySeparatorChar == '\\')
                actualPath = path.Replace('/', Path.DirectorySeparatorChar);

            return actualPath;
        }

        public string GetArchivePath(string path, string workingDirectory, string installDirectory)
        {
            path = GetLocalPath(path, installDirectory);
            workingDirectory = GetLocalPath(workingDirectory, installDirectory);

            var archivePath = path.Replace(workingDirectory, "").Trim(Path.DirectorySeparatorChar);

            if (Path.DirectorySeparatorChar == '\\')
                archivePath = archivePath.Replace(Path.DirectorySeparatorChar, '/');

            return archivePath;
        }

        private void ExtractFilesFromZip(string zipPath, string destination)
        {
            using (var fs = File.OpenRead(zipPath))
            using (var ts = new TrackableStream(fs, fs.Length))
            using (var reader = ReaderFactory.OpenReader(ts, new ReaderOptions()))
            {
                reader.WriteAllToDirectory(destination, new ExtractionOptions()
                {
                    ExtractFullPath = true,
                    Overwrite = true
                });
            }
        }
    }
}
