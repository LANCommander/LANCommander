namespace LANCommander.Packaging.Analysis;

public static class DirectoryScanner
{
    /// <summary>
    /// Records every file under <paramref name="rootPath"/>.
    /// </summary>
    /// <remarks>
    public static DirectorySnapshot Capture(string rootPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            return DirectorySnapshot.Missing(rootPath ?? string.Empty);

        var root = new DirectoryInfo(rootPath);

        if (!root.Exists)
            return DirectorySnapshot.Missing(rootPath);

        var files = new Dictionary<string, FileFingerprint>(StringComparer.OrdinalIgnoreCase);

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };

        foreach (var file in root.EnumerateFiles("*", options))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                files[file.FullName] = new FileFingerprint(file.Length, file.LastWriteTimeUtc);
            }
            catch (IOException)
            {
                // Deleted between being listed and being read. Treating it as absent is right:
                // it is not there now.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return new DirectorySnapshot
        {
            RootPath = root.FullName,
            Files = files,
        };
    }

    public static Task<DirectorySnapshot> CaptureAsync(
        string rootPath, CancellationToken cancellationToken = default) =>
        Task.Run(() => Capture(rootPath, cancellationToken), cancellationToken);

    /// <summary>
    /// Compares two snapshots of the same directory.
    /// </summary>
    public static DirectoryDiff Diff(DirectorySnapshot before, DirectorySnapshot after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        var root = after.RootPath;

        var added = new List<DirectoryChange>();
        var modified = new List<DirectoryChange>();
        var removed = new List<DirectoryChange>();

        foreach (var (path, fingerprint) in after.Files)
        {
            if (!before.Files.TryGetValue(path, out var previous))
            {
                added.Add(Describe(DirectoryChangeKind.Added, root, path, fingerprint.Length, 0));

                continue;
            }

            if (previous.Length != fingerprint.Length ||
                previous.LastWriteTimeUtc != fingerprint.LastWriteTimeUtc)
            {
                modified.Add(Describe(
                    DirectoryChangeKind.Modified, root, path, fingerprint.Length, previous.Length));
            }
        }

        if (before.RootExists)
        {
            foreach (var (path, fingerprint) in before.Files)
            {
                if (!after.Files.ContainsKey(path))
                {
                    removed.Add(Describe(
                        DirectoryChangeKind.Removed, root, path, fingerprint.Length, fingerprint.Length));
                }
            }
        }

        return new DirectoryDiff
        {
            RootPath = root,
            Added = Sort(added),
            Modified = Sort(modified),
            Removed = Sort(removed),
        };
    }

    private static DirectoryChange Describe(
        DirectoryChangeKind kind, string root, string path, long length, long previousLength) => new()
    {
        Kind = kind,
        Path = path,
        RelativePath = RelativeTo(root, path),
        Length = length,
        PreviousLength = previousLength,
    };

    /// <summary>
    /// Falls back to the full path when the file is not under the root, which happens only if
    /// two snapshots of different directories are compared.
    /// </summary>
    private static string RelativeTo(string root, string path)
    {
        if (string.IsNullOrEmpty(root))
            return path;

        try
        {
            var relative = Path.GetRelativePath(root, path);

            return relative.StartsWith("..", StringComparison.Ordinal) ? path : relative;
        }
        catch (ArgumentException)
        {
            return path;
        }
    }

    /// <summary>
    /// Ordered by path so a rescan presents the same list in the same place every time, rather
    /// than in whatever order the dictionary happened to enumerate.
    /// </summary>
    private static List<DirectoryChange> Sort(List<DirectoryChange> changes)
    {
        changes.Sort((a, b) => string.Compare(a.RelativePath, b.RelativePath, StringComparison.OrdinalIgnoreCase));

        return changes;
    }
}
