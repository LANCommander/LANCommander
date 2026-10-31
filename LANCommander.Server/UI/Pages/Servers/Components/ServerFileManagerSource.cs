using LANCommander.Server.Services;
using LANCommander.Server.Services.Models;
using LANCommander.Server.Services.Utilities;
using LANCommander.Server.UI.Components;

namespace LANCommander.Server.UI.Pages.Servers.Components;

/// <summary>
/// A game server's files, through its engine, for the <c>FileManager</c>. Paths are
/// <see cref="ServerFilePath"/>s, so the top is "".
/// </summary>
/// <remarks>
/// The file manager asks its sources synchronously, while a plugin's engine may be reaching another
/// machine, so each call waits for the engine on the thread pool rather than the circuit's context,
/// which it would otherwise deadlock.
/// </remarks>
public sealed class ServerFileManagerSource(ServerManager serverManager, Guid serverId, string rootName) : IFileManagerSource
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly FileManagerDirectory _root = new() { Name = rootName, Path = "" };
    private FileManagerDirectory? _currentPath;

    public string DirectorySeparatorCharacter { get; set; } = "/";

    public FileManagerDirectory GetCurrentPath() => _currentPath ?? _root;

    public void SetCurrentPath(FileManagerDirectory path) => _currentPath = path;

    public IEnumerable<FileManagerDirectory> GetDirectoryTree()
    {
        // Lists the top, so a server whose files can't be reached says why before anything is shown
        _root.Children = Directories(_root, List(""));
        _root.IsExpanded = true;

        return [_root];
    }

    public FileManagerDirectory ExpandNode(FileManagerDirectory node)
    {
        node.IsExpanded = true;

        // Each child's own folders, so the tree knows which of them can be expanded in turn
        foreach (var child in node.Children)
        {
            try
            {
                child.Children = Directories(child, List(child.Path));
            }
            catch
            {
                // It just can't be expanded; opening it says why
            }
        }

        return node;
    }

    public IEnumerable<IFileManagerEntry> GetEntries()
    {
        var current = GetCurrentPath();

        return List(current.Path).Select(file => ToEntry(file, current)).ToList();
    }

    public string GetEntryName(IFileManagerEntry entry) =>
        !String.IsNullOrWhiteSpace(entry.Name) ? entry.Name : ServerFilePath.GetName(entry.Path);

    public FileManagerDirectory GetDirectory(string path) => new()
    {
        Name = ServerFilePath.GetName(path),
        Path = ServerFilePath.Normalize(path),
        Parent = _currentPath,
    };

    public FileManagerFile GetFile(string path) => new()
    {
        Name = ServerFilePath.GetName(path),
        Path = ServerFilePath.Normalize(path),
        Parent = _currentPath,
    };

    public FileManagerDirectory CreateDirectory(string name)
    {
        var path = ServerFilePath.Combine(GetCurrentPath().Path, name);

        Wait(token => serverManager.CreateDirectoryAsync(serverId, path, token));

        return GetDirectory(path);
    }

    public void DeleteEntry(IFileManagerEntry entry) =>
        Wait(token => serverManager.DeleteFileAsync(serverId, entry.Path, token));

    private IReadOnlyList<ServerFile> List(string path) =>
        Wait(token => serverManager.ListFilesAsync(serverId, path, token));

    private static HashSet<FileManagerDirectory> Directories(FileManagerDirectory parent, IEnumerable<ServerFile> files) =>
        files.Where(file => file.IsDirectory).Select(file => (FileManagerDirectory)ToEntry(file, parent)).ToHashSet();

    private static IFileManagerEntry ToEntry(ServerFile file, FileManagerDirectory parent)
    {
        FileManagerEntry entry = file.IsDirectory ? new FileManagerDirectory() : new FileManagerFile();

        entry.Name = file.Name;
        entry.Path = file.Path;
        entry.Size = file.Size ?? 0;
        entry.ModifiedOn = file.LastModified?.ToLocalTime() ?? default;
        entry.CreatedOn = entry.ModifiedOn;
        entry.Parent = parent;

        return entry;
    }

    private static T Wait<T>(Func<CancellationToken, Task<T>> call)
    {
        using var timeout = new CancellationTokenSource(Timeout);

        return Task.Run(() => call(timeout.Token)).GetAwaiter().GetResult();
    }

    private static void Wait(Func<CancellationToken, Task> call)
    {
        using var timeout = new CancellationTokenSource(Timeout);

        Task.Run(() => call(timeout.Token)).GetAwaiter().GetResult();
    }
}
