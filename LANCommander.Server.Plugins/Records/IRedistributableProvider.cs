namespace LANCommander.Server.Plugins;

/// <summary>
/// Software games need installed alongside them, such as a runtime. Games link to redistributables
/// through <see cref="IGameProvider.SetLinksAsync"/>, or a game package's manifest.
/// </summary>
public sealed record RedistributableInfo
{
    /// <summary>Empty when adding a redistributable, to have the server choose one.</summary>
    public Guid Id { get; init; }

    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public string? Notes { get; init; }

    /// <summary>The options games can pass it, as an option schema, or null.</summary>
    public string? OptionSchema { get; init; }

    /// <summary>The version of the newest archive, if any. Set by the server; ignored when writing.</summary>
    public string? Version { get; init; }

    /// <summary>Set by the server; ignored when writing.</summary>
    public DateTime CreatedOn { get; init; }

    /// <summary>Set by the server; ignored when writing.</summary>
    public DateTime UpdatedOn { get; init; }
}

/// <summary>
/// Lists and manages the server's redistributables. Provided by the server; inject it into a scoped
/// plugin service or page. Their archives arrive through <see cref="IPackageImporter"/>, and their
/// scripts are managed through <see cref="IScriptProvider"/>.
/// </summary>
public interface IRedistributableProvider
{
    /// <summary>Every redistributable, ordered by name.</summary>
    Task<IReadOnlyList<RedistributableInfo>> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>The redistributable with the given id, or null.</summary>
    Task<RedistributableInfo?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The redistributable with the given name, compared case-insensitively, or null.</summary>
    Task<RedistributableInfo?> FindAsync(string name, CancellationToken cancellationToken = default);

    Task<RedistributableInfo> AddAsync(RedistributableInfo redistributable, CancellationToken cancellationToken = default);

    /// <summary>Writes every field of a redistributable.</summary>
    /// <exception cref="KeyNotFoundException">There is no such redistributable.</exception>
    Task<RedistributableInfo> UpdateAsync(RedistributableInfo redistributable, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a redistributable along with its archives, files included, unlinking it from every
    /// game. Does nothing when there is no such redistributable.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
