using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;
using Steamworks;
using System;
using System.Collections.Generic;
using System.IO;

namespace LANCommander.SDK.Services
{
    public class LobbyService
    {
        private readonly ILogger Logger;
        private readonly Client Client;

        public LobbyService(Client client)
        {
            Client = client;
        }

        public LobbyService(Client client, ILogger logger)
        {
            Client = client;
            Logger = logger;
        }

        /// <summary>
        /// Get all Steam lobbies for a specified game. Game install directory must contain a file called steam_appid.txt.
        /// Remember to call ReleaseSteam() when the game is done playing or lobby join is canceled!
        /// </summary>
        /// <param name="installDirectory">Directory that contains the game install and steam_appid.txt</param>
        /// <param name="gameId">Game GUID</param>
        /// <returns>Lobby information</returns>
        public IEnumerable<Lobby> GetSteamLobbies(string installDirectory, Guid gameId)
        {
            uint appId = 0;
            var appIdDefinitions = Directory.EnumerateFiles(installDirectory, "steam_appid.txt", SearchOption.AllDirectories);

            foreach (var appIdDefinition in appIdDefinitions)
            {
                if (uint.TryParse(File.ReadAllText(appIdDefinition), out appId))
                    break;
            }

            var lobbies = new List<Lobby>();

            try
            {
                Logger?.LogTrace("Initializing Steamworks with app ID {AppId}", appId);

                SteamClient.Init(appId, true);

                foreach (var friend in SteamFriends.GetFriends())
                {
                    if (friend.IsPlayingThisGame && friend.GameInfo.HasValue && friend.GameInfo.Value.Lobby.HasValue)
                    {
                        var lobby = new Lobby
                        {
                            GameId = gameId,
                            ExternalGameId = appId.ToString(),
                            Id = friend.GameInfo.Value.Lobby.Value.Id.Value.ToString(),
                            ExternalUserId = friend.Id.Value.ToString(),
                            ExternalUsername = friend.Name,
                        };

                        lobbies.Add(lobby);

                        Logger?.LogTrace("Found lobby | {FriendName} ({FriendId}): {LobbyId}", lobby.ExternalUsername, lobby.ExternalUserId, lobby.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Couldn't initialize Steamworks");
            }

            return lobbies;
        }

        public void ReleaseSteam()
        {
            try
            {
                SteamClient.Shutdown();
            }
            catch (Exception ex)
            {

            }
        }
    }
}
