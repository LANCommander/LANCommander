using LANCommander.SDK;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;
using LANCommander.SDK.PowerShell;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LANCommander.SDK
{
    public class GameSaveManager
    {
        private readonly Client Client;

        public delegate void OnDownloadProgressHandler(DownloadProgressChangedEventArgs e);
        public event OnDownloadProgressHandler OnDownloadProgress;

        public delegate void OnDownloadCompleteHandler(AsyncCompletedEventArgs e);
        public event OnDownloadCompleteHandler OnDownloadComplete;

        public GameSaveManager(Client client)
        {
            Client = client;
        }

        public void Download(string installDirectory)
        {
            var manifest = ManifestHelper.Read(installDirectory);

            string tempFile = String.Empty;

            if (manifest != null)
            {
                var destination = Client.DownloadLatestSave(manifest.Id, (changed) =>
                {
                    OnDownloadProgress?.Invoke(changed);
                }, (complete) =>
                {
                    OnDownloadComplete?.Invoke(complete);
                });

                tempFile = destination;

                // Go into the archive and extract the files to the correct locations
                try
                {
                    var tempLocation = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

                    Directory.CreateDirectory(tempLocation);

                    ExtractFilesFromZip(tempFile, tempLocation);

                    #region Move files
                    foreach (var savePath in manifest.SavePaths.Where(sp => sp.Type == "File"))
                    {
                        bool inInstallDir = savePath.Path.StartsWith("{InstallDir}");
                        string tempSavePath = Path.Combine(tempLocation, savePath.Id.ToString());

                        var tempSavePathFile = Path.Combine(tempSavePath, savePath.Path.Replace('/', Path.DirectorySeparatorChar).Replace("{InstallDir}\\", ""));

                        destination = Environment.ExpandEnvironmentVariables(savePath.Path.Replace('/', Path.DirectorySeparatorChar).Replace("{InstallDir}", installDirectory));

                        if (File.Exists(tempSavePathFile))
                        {
                            // Is file, move file
                            if (File.Exists(destination))
                                File.Delete(destination);

                            File.Move(tempSavePathFile, destination);
                        }
                        else if (Directory.Exists(tempSavePath))
                        {
                            var files = Directory.GetFiles(tempSavePath, "*", SearchOption.AllDirectories);

                            foreach (var file in files)
                            {
                                if (inInstallDir)
                                {
                                    // Files are in the game's install directory. Move them there from the save path.
                                    destination = file.Replace(tempSavePath, savePath.Path.Replace('/', Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar).Replace("{InstallDir}", installDirectory));

                                    if (File.Exists(destination))
                                        File.Delete(destination);

                                    File.Move(file, destination);
                                }
                                else
                                {
                                    // Specified path is probably an absolute path, maybe with environment variables.
                                    destination = Environment.ExpandEnvironmentVariables(file.Replace(tempSavePathFile, savePath.Path.Replace('/', '\\')));

                                    if (File.Exists(destination))
                                        File.Delete(destination);

                                    File.Move(file, destination);
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

                        var script = new PowerShellScript();

                        script.UseInline($"regedit.exe /s \"{registryImportFilePath}\"");

                        if (registryImportFileContents.Contains("HKEY_LOCAL_MACHINE"))
                            script.RunAsAdmin();

                        script.Execute();
                    }
                    #endregion

                    // Clean up temp files
                    Directory.Delete(tempLocation, true);
                }
                catch (Exception ex)
                {

                }
            }
        }

        public void Upload(string installDirectory)
        {
            var manifest = ManifestHelper.Read(installDirectory);

            var temp = Path.GetTempFileName();

            if (manifest.SavePaths != null && manifest.SavePaths.Count() > 0)
            {
                using (var archive = ZipArchive.Create())
                {
                    archive.DeflateCompressionLevel = SharpCompress.Compressors.Deflate.CompressionLevel.BestCompression;

                    #region Add files from defined paths
                    foreach (var savePath in manifest.SavePaths.Where(sp => sp.Type == "File"))
                    {
                        IEnumerable<string> localPaths;

                        if (savePath.IsRegex)
                        {
                            var regex = new Regex(Environment.ExpandEnvironmentVariables(savePath.Path.Replace('/', '\\').Replace("{InstallDir}", installDirectory)));
                            
                            localPaths = Directory.GetFiles(installDirectory, "*", SearchOption.AllDirectories)
                                .Where(p => regex.IsMatch(p))
                                .ToList();
                        }
                        else
                            localPaths = new string[] { savePath.Path };

                        var entries = new List<SavePathEntry>();

                        foreach (var localPath in localPaths)
                        {
                            var actualPath = Environment.ExpandEnvironmentVariables(savePath.Path.Replace('/', Path.DirectorySeparatorChar).Replace("{InstallDir}", installDirectory));
                            var relativePath = actualPath.Replace(installDirectory + Path.DirectorySeparatorChar, "");

                            if (Directory.Exists(actualPath))
                            {
                                AddDirectoryToZip(archive, relativePath, actualPath, savePath.Id);
                            }
                            else if (File.Exists(actualPath))
                            {
                                archive.AddEntry(Path.Combine(savePath.Id.ToString(), relativePath), actualPath);
                            }

                            entries.Add(new SavePathEntry
                            {
                                ArchivePath = relativePath,
                                ActualPath = actualPath.Replace(installDirectory, "{InstallDir}")
                            });

                            savePath.Entries = entries;
                        }
                    }
                    #endregion

                    #region Export registry keys
                    if (manifest.SavePaths.Any(sp => sp.Type == "Registry"))
                    {
                        List<string> tempRegFiles = new List<string>();

                        var exportCommand = new StringBuilder();

                        foreach (var savePath in manifest.SavePaths.Where(sp => sp.Type == "Registry"))
                        {
                            var tempRegFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".reg");

                            exportCommand.AppendLine($"reg.exe export \"{savePath.Path.Replace(":\\", "\\")}\" \"{tempRegFile}\"");
                            tempRegFiles.Add(tempRegFile);
                        }

                        var script = new PowerShellScript();

                        script.UseInline(exportCommand.ToString());

                        script.Execute();

                        var exportFile = new StringBuilder();

                        foreach (var tempRegFile in tempRegFiles)
                        {
                            exportFile.AppendLine(File.ReadAllText(tempRegFile));
                            File.Delete(tempRegFile);
                        }

                        archive.AddEntry("_registry.reg", new MemoryStream(Encoding.UTF8.GetBytes(exportFile.ToString())), true);
                    }
                    #endregion

                    var tempManifest = Path.GetTempFileName();

                    File.WriteAllText(tempManifest, ManifestHelper.Serialize(manifest));

                    archive.AddEntry("_manifest.yml", tempManifest);

                    using (var ms = new MemoryStream())
                    {
                        archive.SaveTo(ms);

                        ms.Seek(0, SeekOrigin.Begin);

                        var save = Client.UploadSave(manifest.Id.ToString(), ms.ToArray());
                    }
                }
            }
        }

        private void AddDirectoryToZip(ZipArchive zipArchive, string path, string workingDirectory, Guid pathId)
        {
            foreach (var file in Directory.GetFiles(path))
            {
                // Oh man is this a hack. We should be removing only the working directory from the start,
                // but we're making the assumption that the working dir put in actually prefixes the path.
                // Also wtf, that Path.Combine is stripping the pathId out?
                zipArchive.AddEntry(Path.Combine(pathId.ToString(), path.Substring(workingDirectory.Length), Path.GetFileName(file)), file);
            }

            foreach (var child in Directory.GetDirectories(path))
            {
                // See above
                //ZipEntry entry = new ZipEntry(Path.Combine(pathId.ToString(), path.Substring(workingDirectory.Length), Path.GetFileName(path)));

                //zipStream.PutNextEntry(entry);
                //zipStream.CloseEntry();

                AddDirectoryToZip(zipArchive, child, workingDirectory, pathId);
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
