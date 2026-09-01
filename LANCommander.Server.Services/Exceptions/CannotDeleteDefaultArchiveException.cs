namespace LANCommander.Server.Services.Exceptions;

/// <summary>
/// Thrown when an attempt is made to delete an archive that is still a game's explicit default
/// (<see cref="Data.Models.Game.DefaultArchiveId"/>). The admin UI must require clearing or
/// reassigning the default first (see ArchiveEditor's "Use Latest Automatically"/"Set Default"
/// actions); this is the service-level backstop so bypassing the UI cannot leave a game silently
/// falling back onto the database's ON DELETE SET NULL behavior instead of a deliberate choice.
/// </summary>
public class CannotDeleteDefaultArchiveException(Guid gameId, Guid archiveId) : Exception(
    $"Archive '{archiveId}' cannot be deleted because it is the explicit default archive for game '{gameId}'. Clear or reassign the default first.")
{
    public readonly Guid GameId = gameId;
    public readonly Guid ArchiveId = archiveId;
}
