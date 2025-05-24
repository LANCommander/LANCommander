using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Writers;

namespace LANCommander.SDK.Utilities;

public class SavePacker : IDisposable
{
    private string _installDirectory;
    private MemoryStream _stream;
    private ZipArchive _archive;

    /// <summary>
    /// Initialize the save packer, relying on the manifest for paths
    /// </summary>
    /// <param name="installDirectory">Root installation directory of the package, where the .lancommander directory is located.</param>
    public SavePacker(string installDirectory)
    {
        _installDirectory = installDirectory;
        _stream = new MemoryStream();
        _archive = ZipArchive.Create();
    }

    public SavePacker AddPath(SavePath path)
    {
        switch (path.Type)
        {
            case SavePathType.File:
                AddEntriesFromPath(path);
                break;
            
            case SavePathType.Registry:
                AddRegistryPath(path);
                break;
            
            default:
                throw new ArgumentOutOfRangeException(nameof(path.Type), path.Type, null);
        }

        return this;
    }

    public SavePacker AddPaths(IEnumerable<SavePath> paths)
    {
        foreach (var path in paths)
        {
            AddPath(path);
        }

        return this;
    }

    public SavePacker AddRegistryPath(SavePath registryPath)
    {
        throw new NotImplementedException("Registry stuff has to be rebuilt to avoid reg.exe");
        
        /*
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
         */

        return this;
    }

    public async Task AddManifestAsync(GameManifest manifest)
    {
        _archive.AddEntry(ManifestHelper.ManifestFilename, new MemoryStream(Encoding.UTF8.GetBytes(ManifestHelper.Serialize(manifest))));
    }

    public async Task<Stream> PackAsync()
    {
        _archive.SaveTo(_stream, new WriterOptions(CompressionType.None)
        {
            ArchiveEncoding = new ArchiveEncoding() { Default = Encoding.UTF8 },
            LeaveStreamOpen = true,
        });
        
        _stream.Position = 0;
        
        return _stream;
    }

    private void AddEntriesFromPath(SavePath savePath)
    {
        if (savePath.IsRegex)
            AddEntriesFromFilePathPattern(savePath.Id, savePath.Path, savePath.WorkingDirectory);
        else
            AddEntriesFromFilePath(savePath.Id, savePath.Path, savePath.WorkingDirectory);
    }

    private void AddEntriesFromFilePath(Guid savePathId, string path, string workingDirectory)
    {
        var absoluteLocalWorkingDirectory = workingDirectory.ExpandEnvironmentVariables(_installDirectory);
        var absoluteLocalPath = path.ExpandEnvironmentVariables(_installDirectory);
        var absoluteFullLocalPath = Path.Combine(absoluteLocalWorkingDirectory, absoluteLocalPath);

        var archiveBaseFolder = $"Files/{savePathId}/";
        if (Directory.Exists(absoluteFullLocalPath))
        {
            foreach (var file in Directory.GetFiles(absoluteFullLocalPath, "*", SearchOption.AllDirectories))
            {
                var archivePath = $"{archiveBaseFolder}{GetArchiveEntryName(file, absoluteLocalWorkingDirectory).TrimStart('/')}";
                _archive.AddEntry(archivePath, file);
            }
        }
        else if (File.Exists(absoluteFullLocalPath))
        {
            var archivePath = $"{archiveBaseFolder}{GetArchiveEntryName(absoluteFullLocalPath, absoluteLocalWorkingDirectory).TrimStart('/')}";
            _archive.AddEntry(archivePath, absoluteFullLocalPath);
        }
    }

    private void AddEntriesFromFilePathPattern(Guid savePathId, string pathPattern, string workingDirectory)
    {
        var absoluteLocalWorkingDirectory = workingDirectory.ExpandEnvironmentVariables(_installDirectory);
        
        if (String.IsNullOrWhiteSpace(workingDirectory))
            absoluteLocalWorkingDirectory = _installDirectory;

        if (Path.DirectorySeparatorChar == '\\')
        {
            pathPattern = pathPattern.Replace("\\", "\\\\");
            pathPattern = pathPattern.Replace("/", "\\\\");
        }

        var regex = new Regex(pathPattern);
        
        var matchedFiles = Directory
            .GetFiles(absoluteLocalWorkingDirectory.Replace('/', Path.DirectorySeparatorChar), "*", SearchOption.AllDirectories)
            .Where(f => regex.IsMatch(f.Substring(absoluteLocalWorkingDirectory.Length).TrimStart(Path.DirectorySeparatorChar)))
            .ToList();

        foreach (var file in matchedFiles)
        {
            AddEntriesFromFilePath(savePathId, file, workingDirectory);
        }
    }

    private IEnumerable<SavePathEntry> GetEntriesFromPath(SavePath path)
    {
        if (path.IsRegex)
            return GetEntriesFromFilePattern(path);
        else
            return GetEntriesFromFilePath(path);
    }

    private IEnumerable<SavePathEntry> GetEntriesFromFilePath(SavePath path)
    {
        var absoluteLocalWorkingDirectory = path.WorkingDirectory.ExpandEnvironmentVariables(_installDirectory);
        var absoluteLocalPath = path.Path.ExpandEnvironmentVariables(_installDirectory);
        var absoluteFullLocalPath = Path.Combine(absoluteLocalWorkingDirectory, absoluteLocalPath);
        
        var workingDirectorySanitized = SanitizeLocalPathNameForPacking(absoluteLocalWorkingDirectory);
        var pathSanitized = SanitizeLocalPathNameForPacking(absoluteLocalPath);
        var entries = new List<SavePathEntry>();

        // If target is a directory
        if (Directory.Exists(absoluteFullLocalPath))
        {
            foreach (var file in Directory.GetFiles(absoluteFullLocalPath, "*", SearchOption.AllDirectories))
            {
                entries.Add(new SavePathEntry
                {
                    ArchivePath = GetArchiveEntryName(file, absoluteLocalWorkingDirectory),
                    ActualPath = SanitizeLocalPathNameForPacking(file),
                });
            }
        }
        // Target is a file
        else
        {
            entries.Add(new SavePathEntry
            {
                ArchivePath = GetArchiveEntryName(absoluteFullLocalPath, absoluteLocalWorkingDirectory),
                ActualPath = SanitizeLocalPathNameForPacking(absoluteFullLocalPath),
            });
        }

        return entries;
    }

    private IEnumerable<SavePathEntry> GetEntriesFromFilePattern(SavePath path)
    {
        if (!path.IsRegex)
            throw new ArgumentException("Path is not a regular expression");
        
        var workingDirectory = SanitizeLocalPathNameForPacking(path.WorkingDirectory);
        var pattern = path.Path;
        
        if (String.IsNullOrWhiteSpace(workingDirectory))
            workingDirectory = _installDirectory;

        if (Path.DirectorySeparatorChar == '\\')
        {
            pattern = pattern.Replace("\\", "\\\\");
            pattern = pattern.Replace("/", "\\\\");
        }

        var regex = new Regex(pattern);
        
        var matchedFiles = Directory
            .GetFiles(workingDirectory.Replace('/', Path.DirectorySeparatorChar), "*", SearchOption.AllDirectories)
            .Where(f => regex.IsMatch(f.Substring(workingDirectory.Length).TrimStart(Path.DirectorySeparatorChar)))
            .ToList();
        
        return matchedFiles.Select(f =>
        {
            return new SavePathEntry
            {
                ArchivePath = GetArchiveEntryName(f, workingDirectory),
                ActualPath = SanitizeLocalPathNameForPacking(f)
            };
        });
    }

    private string SanitizeLocalPathNameForPacking(string filePath)
    {
        return filePath
            .DeflateEnvironmentVariables(_installDirectory)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace('\\', '/');
    }

    private string GetArchiveEntryName(string filePath, string workingDirectory)
    {
        var actualPath = SanitizeLocalPathNameForPacking(filePath).TrimStart('/');
        
        workingDirectory = workingDirectory
            .DeflateEnvironmentVariables(_installDirectory)
            .Replace(Path.DirectorySeparatorChar, '/')
            .TrimStart('/');

        if (actualPath.StartsWith(workingDirectory))
            return actualPath.Substring(workingDirectory.Length);
        else
            return actualPath;
    }

    public void Dispose()
    {
        _archive.Dispose();
        //_writer.Dispose();
        _stream.Dispose();
    }
}