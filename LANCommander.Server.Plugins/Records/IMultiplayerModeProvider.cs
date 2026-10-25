using LANCommander.SDK.Enums;

namespace LANCommander.Server.Plugins;

/// <summary>A way a game can be played with others.</summary>
public sealed record MultiplayerModeInfo
{
    public Guid Id { get; init; }
    public MultiplayerType Type { get; init; }
    public NetworkProtocol NetworkProtocol { get; init; }
    public string? Description { get; init; }
    public int MinPlayers { get; init; }
    public int MaxPlayers { get; init; }
    public int Spectators { get; init; }

    /// <summary>Set by the server; ignored when writing.</summary>
    public RecordOwner Owner { get; init; }
}

/// <summary>
/// Manages the multiplayer modes of games. Provided by the server; inject it into a scoped plugin
/// service or page.
/// </summary>
public interface IMultiplayerModeProvider : IOwnedRecordProvider<MultiplayerModeInfo>;
