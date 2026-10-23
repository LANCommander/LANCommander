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
using SharpCompress.Writers.Zip;

namespace LANCommander.SDK.Utilities;

public class SavePacker : IDisposable
{
    private readonly string _installDirectory;
    private readonly MemoryStream _stream;
    private readonly IWritableArchive<ZipWriterOptions> _archive;

    /// <summary>
    /// Initialize the save packer, relying on the manifest for paths
    /// </summary>
    /// <param name="installDirectory">Root installation directory of the package, where the .lancommander directory is located.</param>
    public SavePacker(string installDirectory)
    {
        _installDirectory = installDirectory;
        _stream = new MemoryStream();
        _archive = ZipArchive.CreateArchive();
    }

    public SavePacker AddPath(SDK.Models.Manifest.SavePath path)
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

    public SavePacker AddPaths(IEnumerable<SDK.Models.Manifest.SavePath> paths)
    {
        foreach (var path in paths)
        {
            AddPath(path);
        }

        return this;
    }

    public SavePacker AddRegistryPath(SDK.Models.Manifest.SavePath registryPath)
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

    public async Task AddManifestAsync(Models.Manifest.Game manifest)
    {
        _archive.AddEntry(ManifestHelper.ManifestFilename, new MemoryStream(Encoding.UTF8.GetBytes(ManifestHelper.Serialize(manifest))));
    }

    public bool HasEntries()
    {
        return _archive.Entries.Any();
    }

    public int EntryCount => _archive.Entries.Count();

    public bool HasManifest()
    {
        return _archive.Entries.Any(entry => string.Equals(ManifestHelper.ManifestFilename, entry.Key, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<Stream> PackAsync()
    {
        _archive.SaveTo(_stream, new ZipWriterOptions(CompressionType.None)
        {
            ArchiveEncoding = new ArchiveEncoding() { Default = Encoding.UTF8 },
            LeaveStreamOpen = true,
        });
        
        _stream.Position = 0;
        
        return _stream;
    }

    private void AddEntriesFromPath(SDK.Models.Manifest.SavePath savePath)
    {
        var workingDirectory = GetWorkingDirectory(savePath.WorkingDirectory);

        if (savePath.IsRegex)
            AddEntriesFromFilePathPattern(savePath.Id, savePath.Path, workingDirectory);
        else
            AddEntriesFromFilePath(savePath.Id, savePath.Path, workingDirectory);
    }

    /// <summary>
    /// Save paths without a working directory are relative to the install directory
    /// </summary>
    internal static string GetWorkingDirectory(string workingDirectory)
    {
        return string.IsNullOrWhiteSpace(workingDirectory) ? "{InstallDir}" : workingDirectory;
    }

    /// <summary>
    /// Gets the name of a file's entry in the archive (under Files/{savePathId}/). Files within the working
    /// directory are stored relative to it, anything else is stored by its deflated full path.
    /// </summary>
    internal static string GetArchiveEntryName(string localPath, string localWorkingDirectory, string installDirectory)
    {
        var relativePath = Path.GetRelativePath(localWorkingDirectory, localPath);

        if (relativePath == ".")
            return string.Empty;

        if (relativePath != ".." && !relativePath.StartsWith(".." + Path.DirectorySeparatorChar) && !Path.IsPathRooted(relativePath))
            return relativePath.Replace('\\', '/');

        return localPath
            .DeflateEnvironmentVariables(installDirectory)
            .Replace('\\', '/')
            .TrimStart('/');
    }

    /// <summary>
    /// Expands variables and normalizes separators, as save paths may have been written for another OS
    /// </summary>
    private string GetLocalPath(string path)
    {
        var localPath = path.ExpandEnvironmentVariables(_installDirectory);

        if (Path.DirectorySeparatorChar == '/')
            return localPath.Replace('\\', Path.DirectorySeparatorChar);

        return localPath.Replace('/', Path.DirectorySeparatorChar);
    }

    private void AddEntriesFromFilePath(Guid savePathId, string path, string workingDirectory)
    {
        var absoluteLocalWorkingDirectory = GetLocalPath(workingDirectory);
        var absoluteLocalPath = GetLocalPath(path);
        var absoluteFullLocalPath = Path.Combine(absoluteLocalWorkingDirectory, absoluteLocalPath);

        var archiveBaseFolder = $"Files/{savePathId}/";
        if (Directory.Exists(absoluteFullLocalPath))
        {
            foreach (var file in Directory.GetFiles(absoluteFullLocalPath, "*", SearchOption.AllDirectories))
            {
                var archivePath = $"{archiveBaseFolder}{GetArchiveEntryName(file, absoluteLocalWorkingDirectory, _installDirectory)}";
                _archive.AddEntry(archivePath, file);
            }
        }
        else if (File.Exists(absoluteFullLocalPath))
        {
            var archivePath = $"{archiveBaseFolder}{GetArchiveEntryName(absoluteFullLocalPath, absoluteLocalWorkingDirectory, _installDirectory)}";
            _archive.AddEntry(archivePath, absoluteFullLocalPath);
        }
    }

    private void AddEntriesFromFilePathPattern(Guid savePathId, string pathPattern, string workingDirectory)
    {
        var absoluteLocalWorkingDirectory = GetLocalPath(workingDirectory);

        if (!Directory.Exists(absoluteLocalWorkingDirectory))
            return;

        var regex = new Regex(pathPattern);

        // Patterns match against the path relative to the working directory, using / as the separator
        var matchedFiles = Directory
            .GetFiles(absoluteLocalWorkingDirectory, "*", SearchOption.AllDirectories)
            .Where(f => regex.IsMatch(GetArchiveEntryName(f, absoluteLocalWorkingDirectory, _installDirectory)))
            .ToList();

        foreach (var file in matchedFiles)
        {
            AddEntriesFromFilePath(savePathId, file, workingDirectory);
        }
    }

    public void Dispose()
    {
        _archive.Dispose();
        //_writer.Dispose();
        _stream.Dispose();
    }
}