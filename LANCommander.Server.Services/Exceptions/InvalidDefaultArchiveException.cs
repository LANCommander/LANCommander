namespace LANCommander.Server.Services.Exceptions;

/// <summary>
/// Thrown when an archive is set as a game's default archive but does not belong to that game
/// (or does not exist at all).
/// </summary>
public class InvalidDefaultArchiveException(Guid gameId, Guid archiveId) : Exception(
    $"Archive '{archiveId}' cannot be set as the default archive for game '{gameId}' because it does not belong to that game.")
{
    public readonly Guid GameId = gameId;
    public readonly Guid ArchiveId = archiveId;
}
