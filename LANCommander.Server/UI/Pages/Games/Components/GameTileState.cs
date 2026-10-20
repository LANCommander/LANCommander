namespace LANCommander.Server.UI.Pages.Games.Components;

/// <summary>Where one game is in a bulk action run from the Games list, e.g. fetching cover art.</summary>
public enum GameTileState
{
    Queued,
    Working,
    Done,
    NotFound,
    Failed,
}
