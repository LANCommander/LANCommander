namespace LANCommander.Server.Plugins;

/// <summary>An archive of files for a game, redistributable or tool.</summary>
public sealed record ArchiveInfo
{
    public Guid Id { get; init; }
    public string Version { get; init; } = "";
    public Guid StorageLocationId { get; init; }
    public Guid? GameId { get; init; }
    public Guid? GameVersionId { get; init; }
    public Guid? RedistributableId { get; init; }
    public Guid? ToolId { get; init; }
    public long CompressedSize { get; init; }
    public long UncompressedSize { get; init; }
    public DateTime CreatedOn { get; init; }
}

/// <summary>
/// Lists and deletes archives. Provided by the server; inject it into a scoped plugin service or
/// page. Archives are added by importing a package through <see cref="IPackageImporter"/>.
/// </summary>
public interface IArchiveProvider
{
    /// <summary>
    /// The archives of a game, redistributable or tool, newest first. For a game, every version's
    /// archive unless the owner names a version.
    /// </summary>
    /// <exception cref="ArgumentException">The owner is a server, which has no archives.</exception>
    Task<IReadOnlyList<ArchiveInfo>> GetAsync(RecordOwner owner, CancellationToken cancellationToken = default);

    /// <summary>The archive with the given id, or null.</summary>
    Task<ArchiveInfo?> GetArchiveAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Deletes an archive and its file. Does nothing when there is no such archive.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
