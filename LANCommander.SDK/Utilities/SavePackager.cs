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
        if (registryPath.Type != SavePathType.Registry)
            return this;

        // outsource export
        var exporter = new RegistryExportUtility();
        string regFileContent = exporter.Export(registryPath.Path);

        // write out as UTF8 .reg
        var bytes = Encoding.UTF8.GetBytes(regFileContent);
        var file = new MemoryStream(bytes);

        var index = _archive.Entries.Count(x => x.Key?.StartsWith("_registry") ?? false);
        _archive.AddEntry($"_registry{index}.reg", file);
        return this;
    }

    public async Task AddManifestAsync(GameManifest manifest)
    {
        _archive.AddEntry(ManifestHelper.ManifestFilename, new MemoryStream(Encoding.UTF8.GetBytes(ManifestHelper.Serialize(manifest))));
    }

    public bool HasEntries()
    {
        return _archive.Entries.Any();
    }

    public bool HasManifest()
    {
        return _archive.Entries.Any(entry => string.Equals(ManifestHelper.ManifestFilename, entry.Key, StringComparison.OrdinalIgnoreCase));
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
        
        if (string.IsNullOrWhiteSpace(workingDirectory))
            absoluteLocalWorkingDirectory = _installDirectory;

        var regex = new Regex(pathPattern);
        
        var matchedFiles = Directory
            .GetFiles(absoluteLocalWorkingDirectory.Replace('/', Path.DirectorySeparatorChar), "*", SearchOption.AllDirectories)
            .Where(f => regex.IsMatch(f[absoluteLocalWorkingDirectory.Length..].TrimStart(Path.DirectorySeparatorChar)))
            .ToList();

        foreach (var file in matchedFiles)
        {
            AddEntriesFromFilePath(savePathId, file, workingDirectory);
        }
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