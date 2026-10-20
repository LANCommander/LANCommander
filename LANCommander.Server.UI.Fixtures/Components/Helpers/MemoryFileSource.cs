using LANCommander.Server.UI.Components;

namespace LANCommander.Server.UI.Fixtures.Components.Helpers;

/// <summary>
/// A file manager source over a fixed, made-up folder structure, so file browsers render the same
/// on every machine. Paths use '/'; folders end in '/'.
/// </summary>
public sealed class MemoryFileSource : IFileManagerSource
{
    static readonly DateTime Modified = new(2025, 3, 14, 9, 30, 0);

    readonly Dictionary<string, long> _files;
    readonly HashSet<string> _folders;
    readonly List<FileManagerDirectory> _roots;
    FileManagerDirectory _current;

    public string DirectorySeparatorCharacter { get; set; } = "/";

    /// <param name="files">File paths and sizes, e.g. <c>("Games/Arena Blitz/arena.exe", 4_194_304)</c>.</param>
    /// <param name="folders">Extra, possibly empty, folders.</param>
    public MemoryFileSource(IEnumerable<(string Path, long Size)> files, IEnumerable<string>? folders = null)
    {
        _files = files.ToDictionary(f => "/" + f.Path.TrimStart('/'), f => f.Size);
        _folders = ["/"];

        foreach (var path in _files.Keys.Concat((folders ?? []).Select(f => "/" + f.Trim('/') + "/")))
        {
            for (var index = path.IndexOf('/', 1); index > 0; index = path.IndexOf('/', index + 1))
                _folders.Add(path[..(index + 1)]);
        }

        var root = Directory("/", null);

        _roots = [root];
        _current = root;
    }

    /// <summary>A small game library: a few folders of executables, archives, configs and images.</summary>
    public static MemoryFileSource Sample() => new(
    [
        ("Games/Arena Blitz/arena.exe", 4_194_304),
        ("Games/Arena Blitz/arena.cfg", 2_048),
        ("Games/Arena Blitz/maps/blitz01.pk3", 18_874_368),
        ("Games/Neon Drift/drift.exe", 7_340_032),
        ("Games/Neon Drift/readme.txt", 5_120),
        ("Archives/arena-blitz-1.1.2.zip", 734_003_200),
        ("Archives/neon-drift-2.0.zip", 1_288_490_188),
        ("Media/arena-blitz-cover.png", 512_000),
        ("setup.log", 12_288),
    ], ["Backups"]);

    /// <summary>Starts browsing in <paramref name="path"/>, e.g. "Games/Arena Blitz".</summary>
    public MemoryFileSource At(string path)
    {
        var directory = _roots[0];

        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
            directory = directory.Children.First(c => c.Name == segment);

        _current = directory;

        return this;
    }

    public FileManagerDirectory GetCurrentPath() => _current;

    public void SetCurrentPath(FileManagerDirectory path) => _current = path;

    public IEnumerable<FileManagerDirectory> GetDirectoryTree() => _roots;

    public FileManagerDirectory ExpandNode(FileManagerDirectory node) => node;

    public IEnumerable<IFileManagerEntry> GetEntries()
    {
        var path = _current.Path;

        var folders = _folders
            .Where(f => f != path && f.StartsWith(path) && !f[path.Length..].TrimEnd('/').Contains('/'))
            .Select(f => (IFileManagerEntry)Directory(f, _current));

        var files = _files
            .Where(f => f.Key.StartsWith(path) && !f.Key[path.Length..].Contains('/'))
            .Select(f => (IFileManagerEntry)File(f.Key, f.Value));

        return folders.Concat(files).ToList();
    }

    public string GetEntryName(IFileManagerEntry entry) => entry.Name;

    public FileManagerDirectory GetDirectory(string path) => Directory(path, _current);

    public FileManagerFile GetFile(string path) => File(path, _files.GetValueOrDefault(path));

    public FileManagerDirectory CreateDirectory(string name)
    {
        var path = _current.Path + name.Trim('/') + "/";

        _folders.Add(path);

        return Directory(path, _current);
    }

    public void DeleteEntry(IFileManagerEntry entry)
    {
        _files.Remove(entry.Path);
        _folders.RemoveWhere(f => f.StartsWith(entry.Path) && entry is FileManagerDirectory);
    }

    FileManagerDirectory Directory(string path, FileManagerDirectory? parent)
    {
        var directory = new FileManagerDirectory
        {
            Path = path,
            Name = path == "/" ? "Storage" : path.TrimEnd('/').Split('/').Last(),
            Parent = parent,
            ModifiedOn = Modified,
            CreatedOn = Modified,
        };

        directory.Children = _folders
            .Where(f => f != path && f.StartsWith(path) && !f[path.Length..].TrimEnd('/').Contains('/'))
            .Select(f => Directory(f, directory))
            .ToHashSet();

        return directory;
    }

    FileManagerFile File(string path, long size) => new()
    {
        Path = path,
        Name = path.Split('/').Last(),
        Parent = _current,
        Size = size,
        ModifiedOn = Modified.AddDays(-path.Length),
        CreatedOn = Modified.AddDays(-path.Length * 2),
    };
}
