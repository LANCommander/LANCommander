using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK.Models.Manifest;

namespace LANCommander.SDK.Plugins;

/// <summary>
/// Implemented by launcher plugins that can find multiplayer lobbies for an installed game, for
/// example the lobbies a player's friends are hosting on a platform like Steam. Register the
/// implementation in <see cref="IPlugin.ConfigureServices"/>.
/// </summary>
/// <remarks>
/// Providers are asked only for games with a multiplayer mode whose network protocol is
/// <see cref="Enums.NetworkProtocol.Lobby"/>. Each lobby a provider returns becomes an action
/// that runs the game's primary action with the lobby's <see cref="GameLobby.JoinArguments"/>
/// appended. A provider that throws or takes longer than the host allows is logged and skipped;
/// the other providers' lobbies are still offered. A provider holding resources for the game it
/// was asked about can release them on <see cref="Events.GameAfterExitEvent"/>.
/// </remarks>
public interface ILobbyProvider
{
    /// <summary>Returns the lobbies currently available for the game.</summary>
    Task<IEnumerable<GameLobby>> GetLobbiesAsync(LobbyQuery query, CancellationToken cancellationToken);
}

/// <summary>The installed game a <see cref="ILobbyProvider"/> is asked to find lobbies for.</summary>
/// <param name="GameId">Id of the game.</param>
/// <param name="InstallDirectory">Directory the game is installed in.</param>
/// <param name="Manifests">The manifests of the game and its installed addons.</param>
public sealed record LobbyQuery(Guid GameId, string InstallDirectory, IReadOnlyList<Game> Manifests);

/// <summary>A multiplayer lobby the player can join.</summary>
/// <param name="Id">The lobby's id on the platform that hosts it.</param>
/// <param name="HostName">Name of the player hosting the lobby, shown as "Join {HostName}'s lobby".</param>
/// <param name="JoinArguments">
/// Arguments appended to the game's primary action to join the lobby, for example
/// <c>+connect_lobby 109775241055537385</c>.
/// </param>
public sealed record GameLobby(string Id, string HostName, string JoinArguments);
