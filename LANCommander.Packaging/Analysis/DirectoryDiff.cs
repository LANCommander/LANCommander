namespace LANCommander.Packaging.Analysis;

/// <summary>
/// What happened to a directory between two snapshots.
/// </summary>
public sealed class DirectoryDiff
{
    public required string RootPath { get; init; }

    public IReadOnlyList<DirectoryChange> Added { get; init; } = [];

    public IReadOnlyList<DirectoryChange> Modified { get; init; } = [];

    public IReadOnlyList<DirectoryChange> Removed { get; init; } = [];

    /// <summary>Added and modified together: the files a package would need to pick up.</summary>
    public IEnumerable<DirectoryChange> AddedOrModified => Added.Concat(Modified);

    public IEnumerable<DirectoryChange> All => Added.Concat(Modified).Concat(Removed);

    public int TotalCount => Added.Count + Modified.Count + Removed.Count;

    public bool IsEmpty => TotalCount == 0;

    public static DirectoryDiff Empty(string rootPath) => new() { RootPath = rootPath };
}

/// <summary>
/// One file that appeared, changed, or went away.
/// </summary>
public sealed class DirectoryChange
{
    public DirectoryChangeKind Kind { get; init; }

    /// <summary>Absolute path. Still meaningful for <see cref="DirectoryChangeKind.Removed"/>,
    /// where nothing is there any more.</summary>
    public required string Path { get; init; }

    /// <summary>Path relative to the snapshot root, which is what the user recognises.</summary>
    public required string RelativePath { get; init; }

    /// <summary>Size now, or the size it had before removal.</summary>
    public long Length { get; init; }

    /// <summary>Size in the baseline snapshot. Zero for an addition.</summary>
    public long PreviousLength { get; init; }

    /// <summary>Signed size delta, for showing how much a patch grew or shrank a file.</summary>
    public long LengthDelta => Length - PreviousLength;
}

public enum DirectoryChangeKind
{
    Added = 0,
    Modified = 1,
    Removed = 2,
}
