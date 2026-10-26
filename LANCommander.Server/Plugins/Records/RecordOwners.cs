using LANCommander.Server.Services;

namespace LANCommander.Server.Plugins.Records;

/// <summary>Checks the owners plugins name, and resolves the game version they mean.</summary>
internal sealed class RecordOwners(
    GameService games,
    GameVersionService versions,
    RedistributableService redistributables,
    ToolService tools,
    ServerService servers)
{
    /// <exception cref="KeyNotFoundException">There is no such owner, or version of it.</exception>
    public async Task RequireAsync(RecordOwner owner)
    {
        var exists = owner.Kind switch
        {
            RecordOwnerKind.Game => await games.ExistsAsync(owner.Id),
            RecordOwnerKind.Redistributable => await redistributables.ExistsAsync(owner.Id),
            RecordOwnerKind.Tool => await tools.ExistsAsync(owner.Id),
            RecordOwnerKind.Server => await servers.ExistsAsync(owner.Id),
            _ => throw new ArgumentOutOfRangeException(nameof(owner), owner.Kind, null),
        };

        if (!exists)
            throw RecordGuard.NotFound(owner.Kind.ToString().ToLowerInvariant(), owner.Id);

        if (owner is { Kind: RecordOwnerKind.Game, VersionId: { } versionId }
            && !await versions.ExistsAsync(v => v.Id == versionId && v.GameId == owner.Id))
            throw RecordGuard.NotFound($"version of game {owner.Id}", versionId);
    }

    /// <summary>
    /// The game version a game owner means: the one it names, or the latest. Null when it names
    /// none and the game has no versions yet, unless <paramref name="create"/> is set.
    /// </summary>
    public async Task<Guid?> VersionOfAsync(RecordOwner owner, bool create) =>
        owner.VersionId
        ?? (create ? await versions.GetOrCreateLatestIdAsync(owner.Id) : await versions.GetLatestIdAsync(owner.Id));

    /// <summary>The owner a child record points at, preferring the most specific.</summary>
    public static RecordOwner Of(Guid? gameId, Guid? gameVersionId, Guid? redistributableId, Guid? toolId, Guid? serverId) =>
        gameId is { } game ? RecordOwner.Game(game, gameVersionId)
        : redistributableId is { } redistributable ? RecordOwner.Redistributable(redistributable)
        : toolId is { } tool ? RecordOwner.Tool(tool)
        : serverId is { } server ? RecordOwner.Server(server)
        : default;
}
