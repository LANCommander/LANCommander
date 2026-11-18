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
    private const string MetadataDirectoryName = ".lancommander";

    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private readonly string _installDirectory;
    private readonly MemoryStream _stream;
    private readonly IWritableArchive<ZipWriterOptions> _archive;
    private readonly List<SDK.Models.Manifest.SavePath> _installDirectorySavePaths = new();

    // SharpCompress opens every added file as soon as it is added and holds the handle until the
    // archive is disposed. Packing disposes the archive so the save files aren't locked while the
    // packed stream is uploaded, which would otherwise block the game from being uninstalled.
    private bool _packed;
    private int _packedEntryCount;

    /// <summary>
    /// File save paths that match the whole install directory. These almost always point at the
    /// wrong place and pack the entire game instead of its saves.
    /// </summary>
    public IReadOnlyList<SDK.Models.Manifest.SavePath> InstallDirectorySavePaths => _installDirectorySavePaths;

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
        ThrowIfPacked();

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
        ThrowIfPacked();

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
        ThrowIfPacked();

        _archive.AddEntry(ManifestHelper.ManifestFilename, new MemoryStream(Encoding.UTF8.GetBytes(ManifestHelper.Serialize(manifest))));
    }

    public bool HasEntries()
    {
        return EntryCount > 0;
    }

    public int EntryCount => _packed ? _packedEntryCount : _archive.Entries.Count();

    public bool HasManifest()
    {
        ThrowIfPacked();

        return _archive.Entries.Any(entry => string.Equals(ManifestHelper.ManifestFilename, entry.Key, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Writes the archive and releases every save file it read. Entries can't be added afterwards;
    /// packing again returns the same stream.
    /// </summary>
    public async Task<Stream> PackAsync()
    {
        if (!_packed)
        {
            _archive.SaveTo(_stream, new ZipWriterOptions(CompressionType.None)
            {
                ArchiveEncoding = new ArchiveEncoding() { Default = Encoding.UTF8 },
                LeaveStreamOpen = true,
            });

            _packedEntryCount = _archive.Entries.Count();
            _packed = true;

            _archive.Dispose();
        }

        _stream.Position = 0;

        return _stream;
    }

    private void ThrowIfPacked()
    {
        if (_packed)
            throw new InvalidOperationException("The save has already been packed.");
    }

    private bool IsInstallDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(_installDirectory))
            return false;

        return string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(_installDirectory)),
            PathComparison);
    }

    /// <summary>
    /// LANCommander's own metadata (manifests, file lists, scripts) is never part of a save. Restoring
    /// it would overwrite the installed game's metadata.
    /// </summary>
    private bool IsMetadataPath(string file)
    {
        if (string.IsNullOrWhiteSpace(_installDirectory))
            return false;

        var relativePath = Path.GetRelativePath(_installDirectory, file);

        if (relativePath == ".." || relativePath.StartsWith(".." + Path.DirectorySeparatorChar) || Path.IsPathRooted(relativePath))
            return false;

        var firstSegment = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];

        return string.Equals(firstSegment, MetadataDirectoryName, PathComparison);
    }

    private void AddEntriesFromPath(SDK.Models.Manifest.SavePath savePath)
    {
        var workingDirectory = GetWorkingDirectory(savePath.WorkingDirectory);

        if (savePath.IsRegex)
            AddEntriesFromFilePathPattern(savePath, workingDirectory);
        else
            AddEntriesFromFilePath(savePath, savePath.Path, workingDirectory);
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

    private void AddEntriesFromFilePath(SDK.Models.Manifest.SavePath savePath, string path, string workingDirectory)
    {
        var absoluteLocalWorkingDirectory = GetLocalPath(workingDirectory);
        var absoluteLocalPath = GetLocalPath(path);
        var absoluteFullLocalPath = Path.Combine(absoluteLocalWorkingDirectory, absoluteLocalPath);

        var archiveBaseFolder = $"Files/{savePath.Id}/";
        if (Directory.Exists(absoluteFullLocalPath))
        {
            if (IsInstallDirectory(absoluteFullLocalPath))
                _installDirectorySavePaths.Add(savePath);

            foreach (var file in Directory.GetFiles(absoluteFullLocalPath, "*", SearchOption.AllDirectories))
            {
                if (IsMetadataPath(file))
                    continue;

                var archivePath = $"{archiveBaseFolder}{GetArchiveEntryName(file, absoluteLocalWorkingDirectory, _installDirectory)}";
                _archive.AddEntry(archivePath, file);
            }
        }
        else if (File.Exists(absoluteFullLocalPath) && !IsMetadataPath(absoluteFullLocalPath))
        {
            var archivePath = $"{archiveBaseFolder}{GetArchiveEntryName(absoluteFullLocalPath, absoluteLocalWorkingDirectory, _installDirectory)}";
            _archive.AddEntry(archivePath, absoluteFullLocalPath);
        }
    }

    private void AddEntriesFromFilePathPattern(SDK.Models.Manifest.SavePath savePath, string workingDirectory)
    {
        var absoluteLocalWorkingDirectory = GetLocalPath(workingDirectory);

        if (!Directory.Exists(absoluteLocalWorkingDirectory))
            return;

        var regex = new Regex(savePath.Path);

        var candidateFiles = Directory
            .GetFiles(absoluteLocalWorkingDirectory, "*", SearchOption.AllDirectories)
            .Where(f => !IsMetadataPath(f))
            .ToList();

        // Patterns match against the path relative to the working directory, using / as the separator
        var matchedFiles = candidateFiles
            .Where(f => regex.IsMatch(GetArchiveEntryName(f, absoluteLocalWorkingDirectory, _installDirectory)))
            .ToList();

        // A pattern that matches every file in the install directory (e.g. one that isn't anchored)
        // packs the whole game
        if (candidateFiles.Count > 1 && matchedFiles.Count == candidateFiles.Count && IsInstallDirectory(absoluteLocalWorkingDirectory))
            _installDirectorySavePaths.Add(savePath);

        foreach (var file in matchedFiles)
        {
            AddEntriesFromFilePath(savePath, file, workingDirectory);
        }
    }

    public void Dispose()
    {
        if (!_packed)
            _archive.Dispose();

        //_writer.Dispose();
        _stream.Dispose();
    }
}