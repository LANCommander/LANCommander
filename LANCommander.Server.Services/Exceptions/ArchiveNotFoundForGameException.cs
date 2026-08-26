namespace LANCommander.Server.Services.Exceptions;

/// <summary>
/// Thrown when an archive is explicitly requested/selected for a game (for download, manifest
/// retrieval, resolution, or update checks) but the archive does not belong to that game (or does
/// not exist at all). Callers should treat this as a client error (HTTP 400) rather than silently
/// falling back to another archive.
/// </summary>
public class ArchiveNotFoundForGameException(Guid gameId, Guid archiveId) : Exception(
    $"Archive '{archiveId}' does not belong to game '{gameId}'.")
{
    public readonly Guid GameId = gameId;
    public readonly Guid ArchiveId = archiveId;
}
