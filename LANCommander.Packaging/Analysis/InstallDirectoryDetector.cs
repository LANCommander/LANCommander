namespace LANCommander.Packaging.Analysis;

/// <summary>
/// Infers where an installer put the game from the set of files it wrote.
/// </summary>
public static class InstallDirectoryDetector
{
    /// <summary>
    /// Picks the most likely install directory out of a set of written file paths.
    /// </summary>
    /// <remarks>
    /// Uses the common ancestor of every non-system directory rather than the directory with
    /// the most files: installers routinely write the bulk of their content into subdirectories
    /// (Sounds\, _CD\SETUP\), so "most files" picks a subfolder instead of the root.
    /// </remarks>
    /// <returns>The detected directory, or an empty string when nothing usable was captured.</returns>
    public static string Detect(IEnumerable<string> writtenFilePaths, IEnumerable<string>? ignoredPathPrefixes = null)
    {
        var ignored = ignoredPathPrefixes?.ToArray() ?? [];

        var directories = writtenFilePaths
            .Where(p => !string.IsNullOrWhiteSpace(p) && Path.IsPathRooted(p))
            .Select(Path.GetDirectoryName)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!)
            .ToList();

        if (directories.Count == 0)
            return string.Empty;

        var candidates = directories
            .Where(p => !IsIgnored(p, ignored) && !IsSystemPath(p))
            .ToList();

        if (candidates.Count > 0)
        {
            var ancestor = FindCommonAncestor(candidates);

            // A bare drive root means the installer wrote to unrelated places; fall back to
            // whichever directory it used most.
            if (!string.IsNullOrEmpty(ancestor) && !IsDriveRoot(ancestor))
                return ancestor;

            var mostFrequent = candidates
                .GroupBy(p => p, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            if (mostFrequent != null)
                return mostFrequent.Key;
        }

        return directories[0];
    }

    /// <summary>
    /// Longest directory prefix shared by every path, on path-segment boundaries.
    /// </summary>
    public static string FindCommonAncestor(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
            return string.Empty;

        if (paths.Count == 1)
            return paths[0];

        var splits = paths.Select(p => p.Split(Path.DirectorySeparatorChar)).ToList();
        var minLength = splits.Min(s => s.Length);
        var common = new List<string>();

        for (var i = 0; i < minLength; i++)
        {
            var segment = splits[0][i];

            if (!splits.All(s => s[i].Equals(segment, StringComparison.OrdinalIgnoreCase)))
                break;

            common.Add(segment);
        }

        return string.Join(Path.DirectorySeparatorChar, common);
    }

    public static bool IsDriveRoot(string path)
    {
        var root = Path.GetPathRoot(path);

        return string.Equals(
            path.TrimEnd(Path.DirectorySeparatorChar),
            root?.TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsIgnored(string path, IReadOnlyList<string> ignoredPathPrefixes) =>
        ignoredPathPrefixes.Any(prefix =>
            !string.IsNullOrEmpty(prefix) && path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// True for paths inside the Windows directory. Program Files is explicitly not a system
    /// path — plenty of games install there and we want to detect them.
    /// </summary>
    public static bool IsSystemPath(string path)
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        if (!string.IsNullOrEmpty(programFiles) &&
            path.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrEmpty(programFilesX86) &&
            path.StartsWith(programFilesX86, StringComparison.OrdinalIgnoreCase))
            return false;

        var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        return !string.IsNullOrEmpty(systemRoot)
            && path.StartsWith(systemRoot, StringComparison.OrdinalIgnoreCase);
    }
}
