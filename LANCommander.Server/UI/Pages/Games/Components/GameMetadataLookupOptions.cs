namespace LANCommander.Server.UI.Pages.Games.Components;

/// <summary>What <see cref="GameMetadataLookupDialog"/> looks up, and for which game (empty for a new one).</summary>
public sealed record GameMetadataLookupOptions(Guid GameId, string? Search);
