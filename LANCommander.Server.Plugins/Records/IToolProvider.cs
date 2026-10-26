namespace LANCommander.Server.Plugins;

/// <summary>A program players can install alongside the games that use it, such as a mod manager.</summary>
public sealed record ToolInfo
{
    /// <summary>Empty when adding a tool, to have the server choose one.</summary>
    public Guid Id { get; init; }

    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public string? Notes { get; init; }

    /// <summary>Whether the tool is installed with its games without asking.</summary>
    public bool AlwaysInstall { get; init; }

    /// <summary>Set by the server; ignored when writing.</summary>
    public DateTime CreatedOn { get; init; }

    /// <summary>Set by the server; ignored when writing.</summary>
    public DateTime UpdatedOn { get; init; }
}

/// <summary>
/// Lists and manages the server's tools. Provided by the server; inject it into a scoped plugin
/// service or page. Their archives arrive through <see cref="IPackageImporter"/>, and their actions
/// and scripts are managed through <see cref="IActionProvider"/> and <see cref="IScriptProvider"/>.
/// The games a tool is linked to can be set from either side; see <see cref="GameLinkKind.Tools"/>.
/// </summary>
public interface IToolProvider
{
    /// <summary>Every tool, ordered by name.</summary>
    Task<IReadOnlyList<ToolInfo>> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>The tool with the given id, or null.</summary>
    Task<ToolInfo?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The tool with the given name, compared case-insensitively, or null.</summary>
    Task<ToolInfo?> FindAsync(string name, CancellationToken cancellationToken = default);

    Task<ToolInfo> AddAsync(ToolInfo tool, CancellationToken cancellationToken = default);

    /// <summary>Writes every field of a tool.</summary>
    /// <exception cref="KeyNotFoundException">There is no such tool.</exception>
    Task<ToolInfo> UpdateAsync(ToolInfo tool, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a tool along with its archives, files included, unlinking it from every game. Does
    /// nothing when there is no such tool.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The ids of the games a tool is linked to.</summary>
    /// <exception cref="KeyNotFoundException">There is no such tool.</exception>
    Task<IReadOnlyList<Guid>> GetGamesAsync(Guid toolId, CancellationToken cancellationToken = default);

    /// <summary>Replaces the games a tool is linked to. An empty list unlinks them all.</summary>
    /// <exception cref="KeyNotFoundException">The tool, or one of the games, does not exist.</exception>
    Task SetGamesAsync(Guid toolId, IEnumerable<Guid> gameIds, CancellationToken cancellationToken = default);
}
