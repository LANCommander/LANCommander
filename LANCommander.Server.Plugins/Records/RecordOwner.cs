namespace LANCommander.Server.Plugins;

/// <summary>The kinds of record that own actions, scripts and the other child records.</summary>
public enum RecordOwnerKind
{
    Game,
    Redistributable,
    Tool,
    Server,
}

/// <summary>
/// The record a child record (an action, script, save path, archive…) belongs to.
/// </summary>
/// <param name="Kind">What kind of record the owner is.</param>
/// <param name="Id">The owner's id.</param>
/// <param name="VersionId">
/// For a game's actions, scripts and save paths, the game version they belong to. Null means the
/// game's latest version. Ignored for every other kind of owner and child.
/// </param>
public readonly record struct RecordOwner(RecordOwnerKind Kind, Guid Id, Guid? VersionId = null)
{
    /// <summary>A game, or one of its versions.</summary>
    public static RecordOwner Game(Guid id, Guid? versionId = null) => new(RecordOwnerKind.Game, id, versionId);

    public static RecordOwner Redistributable(Guid id) => new(RecordOwnerKind.Redistributable, id);

    public static RecordOwner Tool(Guid id) => new(RecordOwnerKind.Tool, id);

    public static RecordOwner Server(Guid id) => new(RecordOwnerKind.Server, id);
}
