using LANCommander.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.PlaynitePlugin.Extensions
{
    internal static class MultiplayerInfoExtensions
    {
        internal static string GetPlayerCount(this MultiplayerInfo multiplayerInfo)
        {
            if (multiplayerInfo.MinPlayers == multiplayerInfo.MaxPlayers && multiplayerInfo.MinPlayers >= 2)
                return $"({multiplayerInfo.MinPlayers} Players)";

            if (multiplayerInfo.MinPlayers < multiplayerInfo.MaxPlayers && multiplayerInfo.MinPlayers >= 2)
                return $"({multiplayerInfo.MinPlayers}-{multiplayerInfo.MaxPlayers} Players)";

            if (multiplayerInfo.MinPlayers <= 1 && multiplayerInfo.MaxPlayers > 2)
                return $"({multiplayerInfo.MaxPlayers} Players)";

            return "";
        }
    }
}
