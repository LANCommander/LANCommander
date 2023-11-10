using LANCommander.SDK;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;
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

                    var deserializer = new DeserializerBuilder()
                        .WithNamingConvention(new PascalCaseNamingConvention())
                        .Build();

                    #region Move files
                    foreach (var savePath in manifest.SavePaths.Where(sp => sp.Type == "File"))
                    {
                        bool inInstallDir = savePath.Path.StartsWith("{InstallDir}");
                        string tempSavePath = Path.Combine(tempLocation, savePath.Id.ToString());

                        var tempSavePathFile = Path.Combine(tempSavePath, savePath.Path.Replace('/', '\\').Replace("{InstallDir}\\", ""));

                        destination = Environment.ExpandEnvironmentVariables(savePath.Path.Replace('/', '\\').Replace("{InstallDir}", installDirectory));

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

                            if (inInstallDir)
                            {
                                foreach (var file in files)
                                {
                                    if (inInstallDir)
                                    {
                                        // Files are in the game's install directory. Move them there from the save path.
                                        destination = file.Replace(tempSavePath, savePath.Path.Replace('/', '\\').TrimEnd('\\').Replace("{InstallDir}", installDirectory));

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
                            else
                            {

                            }
                        }
                    }
                    #endregion

                    #region Handle registry importing
                    var registryImportFilePath = Path.Combine(tempLocation, "_registry.reg");

                    if (File.Exists(registryImportFilePath))
                    {
                        var registryImportFileContents = File.ReadAllText(registryImportFilePath);

                        PowerShellRuntime.RunCommand($"regedit.exe /s \"{registryImportFilePath}\"", registryImportFileContents.Contains("HKEY_LOCAL_MACHINE"));
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
                        var localPath = Environment.ExpandEnvironmentVariables(savePath.Path.Replace('/', '\\').Replace("{InstallDir}", installDirectory));

                        if (Directory.Exists(localPath))
                        {
                            AddDirectoryToZip(archive, localPath, localPath, savePath.Id);
                        }
                        else if (File.Exists(localPath))
                        {
                            archive.AddEntry(Path.Combine(savePath.Id.ToString(), savePath.Path.Replace("{InstallDir}/", "")), localPath);
                        }
                    }
                    #endregion

                    #region Add files from defined paths
                    foreach (var savePath in manifest.SavePaths.Where(sp => sp.Type == "File"))
                    {
                        var localPath = Environment.ExpandEnvironmentVariables(savePath.Path.Replace('/', '\\').Replace("{InstallDir}", installDirectory));

                        if (Directory.Exists(localPath))
                        {
                            AddDirectoryToZip(archive, localPath, localPath, savePath.Id);
                        }
                        else if (File.Exists(localPath))
                        {
                            archive.AddEntry(Path.Combine(savePath.Id.ToString(), savePath.Path.Replace("{InstallDir}/", "")), localPath);
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

                        PowerShellRuntime.RunCommand(exportCommand.ToString());

                        var exportFile = new StringBuilder();

                        foreach (var tempRegFile in tempRegFiles)
                        {
                            exportFile.AppendLine(File.ReadAllText(tempRegFile));
                            File.Delete(tempRegFile);
                        }

                        archive.AddEntry("_registry.reg", new MemoryStream(Encoding.UTF8.GetBytes(exportFile.ToString())), true);
                    }
                    #endregion

                    archive.AddEntry("_manifest.yml", ManifestHelper.GetPath(installDirectory));

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
