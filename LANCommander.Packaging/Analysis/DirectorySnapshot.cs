namespace LANCommander.Packaging.Analysis;

/// <summary>
/// Everything a directory tree contained at one moment, cheap enough to take twice around a
/// manual patching session.
/// </summary>
/// <remarks>
/// Files are identified by length and last-write time rather than by content hash. A game
/// install is routinely tens of gigabytes and hashing it twice would take longer than the
/// patching it is there to observe. The trade is that a patcher which rewrites a file to the
/// same length <em>and</em> restores its timestamp is invisible to a rescan — running that
/// patcher under instrumentation instead is what covers the case.
/// </remarks>
public sealed class DirectorySnapshot
{
    /// <summary>Directory the snapshot was taken of. Keys in <see cref="Files"/> sit under it.</summary>
    public required string RootPath { get; init; }

    public DateTime CapturedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>Absolute path to fingerprint, compared case-insensitively.</summary>
    public required IReadOnlyDictionary<string, FileFingerprint> Files { get; init; }

    /// <summary>
    /// True when the root did not exist when the snapshot was taken, as opposed to existing and
    /// being empty. A diff against a missing root would otherwise read as "the user deleted
    /// everything".
    /// </summary>
    public bool RootExists { get; init; } = true;

    public int FileCount => Files.Count;

    /// <summary>A snapshot of a directory that is not there.</summary>
    public static DirectorySnapshot Missing(string rootPath) => new()
    {
        RootPath = rootPath,
        Files = new Dictionary<string, FileFingerprint>(StringComparer.OrdinalIgnoreCase),
        RootExists = false,
    };
}

/// <summary>
/// What is compared to decide whether a file changed.
/// </summary>
public readonly record struct FileFingerprint(long Length, DateTime LastWriteTimeUtc);
