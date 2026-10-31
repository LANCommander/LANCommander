using LANCommander.Server.Services.Models;

namespace LANCommander.Server.Services.Utilities;

/// <summary>
/// A game server's files on this machine, in and under one folder, e.g. its working directory.
/// Paths are <see cref="ServerFilePath"/>s, and are checked again here against the folder they resolve
/// to, so nothing outside it can be reached.
/// </summary>
public sealed class LocalServerFiles
{
    private readonly string _root;

    public LocalServerFiles(string root)
    {
        if (String.IsNullOrWhiteSpace(root))
            throw new InvalidOperationException("The server has no working directory. Set one on its General page to manage its files.");

        _root = Path.GetFullPath(root);
    }

    public IReadOnlyList<ServerFile> List(string path)
    {
        var directory = new DirectoryInfo(Resolve(path));

        if (!directory.Exists)
            throw new DirectoryNotFoundException(path.Length == 0
                ? $"The server's working directory {_root} doesn't exist."
                : $"There is no folder {path}.");

        var normalized = ServerFilePath.Normalize(path);

        return directory
            .EnumerateFileSystemInfos("*", new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = 0 })
            .Select(info => new ServerFile(
                info.Name,
                ServerFilePath.Combine(normalized, info.Name),
                info is DirectoryInfo,
                info is FileInfo file ? file.Length : null,
                info.LastWriteTimeUtc))
            .ToList();
    }

    public Stream OpenRead(string path)
    {
        var file = Resolve(path);

        if (!File.Exists(file))
            throw new FileNotFoundException($"There is no file {path}.", path);

        // The server may have it open, e.g. a log it is writing
        return new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 81920, useAsync: true);
    }

    public async Task WriteAsync(string path, Stream content, CancellationToken cancellationToken)
    {
        var file = Resolve(path);

        if (file == _root || Directory.Exists(file))
            throw new IOException($"{path} is a folder.");

        Directory.CreateDirectory(Path.GetDirectoryName(file)!);

        await using var stream = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.Read, 81920, useAsync: true);

        await content.CopyToAsync(stream, cancellationToken);
    }

    public void Delete(string path)
    {
        var target = Resolve(path);

        if (target == _root)
            throw new InvalidOperationException("The server's working directory itself can't be deleted.");

        if (Directory.Exists(target))
            Directory.Delete(target, recursive: true);
        else if (File.Exists(target))
            File.Delete(target);
        else
            throw new FileNotFoundException($"There is no file or folder {path}.", path);
    }

    public void CreateDirectory(string path) => Directory.CreateDirectory(Resolve(path));

    private string Resolve(string path)
    {
        var full = Path.GetFullPath(Path.Combine(_root, ServerFilePath.Normalize(path)));
        var relative = Path.GetRelativePath(_root, full);

        if (relative == ".")
            return _root;

        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar))
            throw new ArgumentException("The path can't leave the server's files.", nameof(path));

        return full;
    }
}
