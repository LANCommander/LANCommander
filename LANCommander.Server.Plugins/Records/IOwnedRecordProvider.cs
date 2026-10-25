namespace LANCommander.Server.Plugins;

/// <summary>
/// Lists and manages records that belong to a game, redistributable, tool or server: actions,
/// scripts, save paths and the like. Each provider accepts only the owners its records can have,
/// and throws <see cref="ArgumentException"/> for any other.
/// </summary>
/// <typeparam name="TInfo">The record.</typeparam>
public interface IOwnedRecordProvider<TInfo> where TInfo : class
{
    /// <summary>
    /// The owner's records. For a game's version-scoped records, those of the version the owner
    /// names, or of the latest version.
    /// </summary>
    /// <exception cref="KeyNotFoundException">There is no such owner.</exception>
    Task<IReadOnlyList<TInfo>> GetAsync(RecordOwner owner, CancellationToken cancellationToken = default);

    /// <summary>The record with the given id, or null.</summary>
    Task<TInfo?> GetRecordAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a record to an owner. For a game's version-scoped records, to the version the owner
    /// names, or to the latest version.
    /// </summary>
    /// <exception cref="KeyNotFoundException">There is no such owner.</exception>
    Task<TInfo> AddAsync(RecordOwner owner, TInfo record, CancellationToken cancellationToken = default);

    /// <summary>Writes every field of a record. It stays with its owner.</summary>
    /// <exception cref="KeyNotFoundException">There is no such record.</exception>
    Task<TInfo> UpdateAsync(TInfo record, CancellationToken cancellationToken = default);

    /// <summary>Deletes a record. Does nothing when there is no such record.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
