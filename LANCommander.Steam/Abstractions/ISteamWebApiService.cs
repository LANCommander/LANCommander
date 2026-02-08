using System.Collections.Generic;
using System.Threading.Tasks;
using LANCommander.Steam.Models.SteamCmdNet;

namespace LANCommander.Steam.Abstractions;

public interface ISteamWebApiService
{
    public Task<AppInfo> GetAppInfo(uint appId);
    public Task<IEnumerable<GameSearchResult>> SearchGamesAsync(string keyword);
}