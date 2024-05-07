using LANCommander.SDK;
using LANCommander.SDK.Models;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.PlaynitePlugin.Extensions
{
    internal static class MultiplayerModeExtensions
    {
        internal static string GetPlayerCount(this MultiplayerMode multiplayerMode)
        {
            if (multiplayerMode.MinPlayers == multiplayerMode.MaxPlayers && multiplayerMode.MinPlayers >= 2)
                return $"({multiplayerMode.MinPlayers} {ResourceProvider.GetString("LOCLANCommanderPlayers")})";

            if (multiplayerMode.MinPlayers < multiplayerMode.MaxPlayers && multiplayerMode.MinPlayers >= 2)
                return $"({multiplayerMode.MinPlayers}-{multiplayerMode.MaxPlayers} {ResourceProvider.GetString("LOCLANCommanderPlayers")})";

            if (multiplayerMode.MinPlayers <= 1 && multiplayerMode.MaxPlayers > 2)
                return $"({multiplayerMode.MaxPlayers} {ResourceProvider.GetString("LOCLANCommanderPlayers")})";

            return "";
        }
    }
}
