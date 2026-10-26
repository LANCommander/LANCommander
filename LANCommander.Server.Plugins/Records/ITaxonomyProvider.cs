namespace LANCommander.Server.Plugins;

/// <summary>The kinds of taxonomy record: named records games are linked to.</summary>
public enum TaxonomyKind
{
    Category,
    Collection,
    /// <summary>A developer or publisher.</summary>
    Company,
    Engine,
    Genre,
    Platform,
    Tag,
}

/// <summary>A taxonomy record on the server.</summary>
public sealed record TaxonomyInfo
{
    public Guid Id { get; init; }
    public TaxonomyKind Kind { get; init; }
    public string Name { get; init; } = "";
}

/// <summary>
/// Lists and manages the server's taxonomy records: categories, collections, companies, engines,
/// genres, platforms and tags. Provided by the server; inject it into a scoped plugin service or
/// page. Games are linked to them through <see cref="IGameProvider"/>.
/// </summary>
public interface ITaxonomyProvider
{
    /// <summary>Every record of a kind, ordered by name.</summary>
    Task<IReadOnlyList<TaxonomyInfo>> GetAsync(TaxonomyKind kind, CancellationToken cancellationToken = default);

    /// <summary>The record with the given id, or null.</summary>
    Task<TaxonomyInfo?> GetAsync(TaxonomyKind kind, Guid id, CancellationToken cancellationToken = default);

    /// <summary>The record with the given name, compared case-insensitively, or null.</summary>
    Task<TaxonomyInfo?> FindAsync(TaxonomyKind kind, string name, CancellationToken cancellationToken = default);

    /// <summary>The record with the given name, added first when there is none.</summary>
    Task<TaxonomyInfo> GetOrAddAsync(TaxonomyKind kind, string name, CancellationToken cancellationToken = default);

    /// <summary>Adds a record, even when one with the same name exists.</summary>
    Task<TaxonomyInfo> AddAsync(TaxonomyKind kind, string name, CancellationToken cancellationToken = default);

    /// <exception cref="KeyNotFoundException">There is no such record.</exception>
    Task<TaxonomyInfo> RenameAsync(TaxonomyKind kind, Guid id, string name, CancellationToken cancellationToken = default);

    /// <summary>Deletes a record, unlinking it from every game. Does nothing when there is no such record.</summary>
    Task DeleteAsync(TaxonomyKind kind, Guid id, CancellationToken cancellationToken = default);
}
