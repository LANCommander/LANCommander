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
using SharpCompress.Writers;
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

// Some terms for this file since they're probabably going to be needed in the future:
// Local path - The full path of the file/directory on the local disk. No variables used, just the raw path for current machine
// Actual path - The path where the entries should be extracted to, before expanding environemnt variables.
// Archive path - The path of where the entries are located in the ZIP
//
// Other notes:
// - Entries in the ZIP are separated by save path ID to avoid collision

namespace LANCommander.SDK.Services
{
    public class SaveService
    {
        private readonly ILogger Logger;

        private readonly Client Client;

        public delegate void OnDownloadProgressHandler(DownloadProgressChangedEventArgs e);
        public event OnDownloadProgressHandler OnDownloadProgress;

        public delegate void OnDownloadCompleteHandler(AsyncCompletedEventArgs e);
        public event OnDownloadCompleteHandler OnDownloadComplete;

        public SaveService(Client client)
        {
            Client = client;
        }

        public SaveService(Client client, ILogger logger)
        {
            Client = client;
            Logger = logger;
        }

        private async Task<string> DownloadAsync(Guid id, Action<DownloadProgressChangedEventArgs> progressHandler, Action<AsyncCompletedEventArgs> completeHandler)
        {
            return await Client.DownloadRequestAsync($"/api/Saves/{id}/Download", progressHandler, completeHandler);
        }

        public async Task<string> DownloadLatestAsync(Guid gameId, Action<DownloadProgressChangedEventArgs> progressHandler, Action<AsyncCompletedEventArgs> completeHandler)
        {
            return await Client.DownloadRequestAsync($"/api/Saves/Game/{gameId}/Latest/Download", progressHandler, completeHandler);
        }

        public IEnumerable<GameSave> Get(Guid gameId)
        {
            return Client.GetRequest<IEnumerable<GameSave>>($"/api/Saves/Game/{gameId}");
        }

        public async Task<IEnumerable<GameSave>> GetAsync(Guid gameId)
        {
            return await Client.GetRequestAsync<IEnumerable<GameSave>>($"/api/Saves/Game/{gameId}");
        }

        public GameSave GetLatest(Guid gameId)
        {
            return Client.GetRequest<GameSave>($"/api/Saves/Game/{gameId}/Latest");
        }

        public Task<GameSave> GetLatestAsync(Guid gameId)
        {
            return Client.GetRequestAsync<GameSave>($"/api/Saves/Game/{gameId}/Latest");
        }

        public GameSave Upload(Guid gameId, byte[] data)
        {
            Logger?.LogTrace("Uploading save...");

            return Client.UploadRequest<GameSave>($"/api/Saves/Game/{gameId}/Upload", gameId.ToString(), data);
        }

        public async Task DownloadAsync(string installDirectory, Guid gameId, Guid? saveId = null)
        {
            var manifest = await ManifestHelper.ReadAsync<GameManifest>(installDirectory, gameId);

            string tempFile = string.Empty;
            string tempLocation = string.Empty;

            if (manifest != null)
            {
                string destination;

                if (!saveId.HasValue)
                {
                    destination = await DownloadLatestAsync(manifest.Id, (changed) =>
                    {
                        OnDownloadProgress?.Invoke(changed);
                    }, (complete) =>
                    {
                        OnDownloadComplete?.Invoke(complete);
                    });
                }
                else
                {
                    destination = await DownloadAsync(saveId.Value, (changed) =>
                    {
                        OnDownloadProgress?.Invoke(changed);
                    }, (complete) =>
                    {
                        OnDownloadComplete?.Invoke(complete);
                    });
                }


                if (string.IsNullOrWhiteSpace(destination))
                    return;

                Logger?.LogTrace("Game save archive downloaded to {SaveTempLocation}", destination);

                tempFile = destination;

                // Go into the archive and extract the files to the correct locations
                try
                {
                    tempLocation = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

                    Directory.CreateDirectory(tempLocation);

                    bool success = RetryHelper.RetryOnException(10, TimeSpan.FromMilliseconds(200), false, () =>
                    {
                        Logger?.LogTrace("Attempting to extracting save entries to the temporary location {TempPath}");

                        ExtractFilesFromZip(tempFile, tempLocation);

                        return true;
                    });

                    if (!success)
                        throw new ExtractionException("Could not extract the save archive. Is the file locked?");

                    manifest = await ManifestHelper.ReadAsync<GameManifest>(tempLocation);

                    #region Move files
                    var tempLocationFilePath = "Files";

                    // Legacy support
                    if (!Directory.Exists(Path.Combine(tempLocation, tempLocationFilePath)))
                        tempLocationFilePath = "Saves";

                    foreach (var savePath in manifest.SavePaths.Where(sp => sp.Type == Enums.SavePathType.File))
                    {
                        foreach (var entry in savePath.Entries)
                        {
                            var entryPath = Path.Combine(tempLocation, tempLocationFilePath, savePath.Id.ToString(), entry.ArchivePath.Replace('/', Path.DirectorySeparatorChar));
                            var destinationPath = entry.ActualPath.ExpandEnvironmentVariables(installDirectory);

                            if (File.Exists(entryPath))
                            {
                                var destinationDirectory = Path.GetDirectoryName(entryPath);

                                Directory.CreateDirectory(destinationDirectory);

                                // Handle individual files that were saved as an entry in the path
                                if (File.Exists(destinationPath))
                                    File.Delete(destinationPath);

                                File.Move(entryPath, destinationPath);
                            }
                            else if (Directory.Exists(entryPath))
                            {
                                // Handle directories that were saved as an entry in the path
                                var entryFiles = Directory.GetFiles(entryPath, "*", SearchOption.AllDirectories);

                                foreach (var entryFile in entryFiles)
                                {
                                    var destinationDirectory = Path.GetDirectoryName(entryFile);

                                    Directory.CreateDirectory(destinationDirectory);

                                    var fileDestination = entryFile.Replace(entryPath, destinationPath);

                                    if (File.Exists(fileDestination))
                                        File.Delete(fileDestination);

                                    File.Move(entryFile, fileDestination);
                                }
                            }
                        }
                    }
                    #endregion

                    #region Handle registry importing
                    var registryImportFilePath = Path.Combine(tempLocation, "_registry.reg");

                    if (File.Exists(registryImportFilePath))
                    {
                        var registryImportFileContents = File.ReadAllText(registryImportFilePath);

                        var script = new PowerShellScript(Enums.ScriptType.Install);

                        script.UseInline($"regedit.exe /s \"{registryImportFilePath}\"");

                        if (registryImportFileContents.Contains("HKEY_LOCAL_MACHINE"))
                            script.AsAdmin();

                        if (Client.Scripts.Debug)
                            script.EnableDebug();

                        await script.ExecuteAsync<int>();
                    }
                    #endregion

                    // Clean up temp files
                    Directory.Delete(tempLocation, true);
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "The files in a save could not be extracted to their destination");
                }
                finally
                {
                    if (Directory.Exists(tempLocation))
                        Directory.Delete(tempLocation, true);
                }
            }
        }

        public async Task UploadAsync(string installDirectory, Guid gameId)
        {
            var manifest = await ManifestHelper.ReadAsync<GameManifest>(installDirectory, gameId);

            if (manifest.SavePaths != null && manifest.SavePaths.Count() > 0)
            {
                using (var ms = new MemoryStream())
                using (var writer = WriterFactory.Open(ms, ArchiveType.Zip, new WriterOptions(CompressionType.Deflate)
                {
                    ArchiveEncoding = new ArchiveEncoding() { Default = Encoding.UTF8 }
                }))
                {
                    #region Add files from defined paths
                    foreach (var savePath in manifest.SavePaths.Where(sp => sp.Type == Enums.SavePathType.File))
                    {
                        savePath.Entries = GetFileSavePathEntries(savePath, installDirectory);

                        foreach (var entry in savePath.Entries)
                        {
                            var localPath = GetLocalPath(entry.ActualPath, installDirectory);

                            if (Directory.Exists(localPath))
                                AddDirectoryToZip(writer, entry.ArchivePath, localPath, savePath.Id);
                            else if (File.Exists(localPath))
                                writer.Write($"Files/{savePath.Id}/{entry.ArchivePath}", localPath);
                        }
                    }
                    #endregion

                    #region Export registry keys
                    if (manifest.SavePaths.Any(sp => sp.Type == Enums.SavePathType.Registry))
                    {
                        List<string> tempRegFiles = new List<string>();

                        Logger?.LogTrace("Building registry export file");

                        var exportCommand = new StringBuilder();

                        foreach (var savePath in manifest.SavePaths.Where(sp => sp.Type == Enums.SavePathType.Registry))
                        {
                            var tempRegFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".reg");

                            exportCommand.AppendLine($"reg.exe export \"{savePath.Path.Replace(":\\", "\\")}\" \"{tempRegFile}\"");
                            tempRegFiles.Add(tempRegFile);
                        }

                        var script = new PowerShellScript(Enums.ScriptType.SaveUpload);

                        script.UseInline(exportCommand.ToString());

                        if (Client.Scripts.Debug)
                            script.EnableDebug();

                        await script.ExecuteAsync<int>();

                        var exportFile = new StringBuilder();

                        foreach (var tempRegFile in tempRegFiles)
                        {
                            exportFile.AppendLine(File.ReadAllText(tempRegFile));
                            File.Delete(tempRegFile);
                        }

                        writer.Write("_registry.reg", new MemoryStream(Encoding.UTF8.GetBytes(exportFile.ToString())));
                    }
                    #endregion

                    writer.Write(ManifestHelper.ManifestFilename, new MemoryStream(Encoding.UTF8.GetBytes(ManifestHelper.Serialize(manifest))));

                    ms.Seek(0, SeekOrigin.Begin);

                    var save = Upload(manifest.Id, ms.ToArray());
                }
            }
        }

        public async Task DeleteAsync(Guid id)
        {
            await Client.DeleteRequestAsync<bool>($"/api/Saves/{id}");
        }

        public IEnumerable<SavePathEntry> GetFileSavePathEntries(SavePath savePath, string installDirectory)
        {
            IEnumerable<string> localPaths;

            if (savePath.IsRegex)
            {
                var workingDirectory = GetLocalPath(savePath.WorkingDirectory, installDirectory);
                var pattern = savePath.Path;

                if (string.IsNullOrWhiteSpace(workingDirectory))
                    workingDirectory = installDirectory;

                if (Path.DirectorySeparatorChar == '\\')
                {
                    pattern = pattern.Replace("\\", "\\\\");
                    pattern = pattern.Replace("/", "\\\\");
                }

                var regex = new Regex(pattern);

                localPaths = Directory.GetFiles(workingDirectory, "*", SearchOption.AllDirectories)
                    .Where(p => regex.IsMatch(p.Substring(workingDirectory.Length).TrimStart(Path.DirectorySeparatorChar)))
                    .ToList();
            }
            else
            {
                var workingDirectory = GetLocalPath(savePath.WorkingDirectory, installDirectory);

                var localPath = Path.Combine(workingDirectory, GetLocalPath(savePath.Path, installDirectory));

                localPaths = new string[] { localPath };
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
                actualPath.Replace(Path.DirectorySeparatorChar, '/');

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

        private void AddDirectoryToZip(IWriter writer, string archivePath, string localPath, Guid pathId)
        {
            foreach (var file in Directory.GetFiles(localPath, "*", SearchOption.AllDirectories))
            {
                var fileArchivePath = file.Substring(localPath.Length).Replace(Path.DirectorySeparatorChar, '/').Trim('/');

                writer.Write($"Files/{pathId}/{archivePath}/{fileArchivePath}", file);
            }
        }

        private void ExtractFilesFromZip(string zipPath, string destination)
        {
            using (var fs = File.OpenRead(zipPath))
            using (var ts = new TrackableStream(fs))
            using (var reader = ReaderFactory.Open(ts))
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
