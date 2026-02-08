using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using HtmlAgilityPack;
using LANCommander.Steam.Abstractions;
using LANCommander.Steam.Models.SteamCmdNet;

namespace LANCommander.Steam.Services;

public class SteamWebApiService(HttpClient httpClient) : ISteamWebApiService
{
    public async Task<AppInfo> GetAppInfo(uint appId)
    {
        var response = await httpClient.GetFromJsonAsync<AppInfoResponse>($"https://api.steamcmd.net/v1/info/{appId}");

        if (response.Data?.ContainsKey(appId) ?? false)
            return response.Data[appId];

        return null;
    }

    public async Task<IEnumerable<GameSearchResult>> SearchGamesAsync(string keyword)
    {
        HtmlWeb web = new HtmlWeb();
        HtmlDocument dom =
            await web.LoadFromWebAsync($"https://store.steampowered.com/search/suggest?term={keyword}&f=games&cc=US");

        List<GameSearchResult> results = [];
        var matches = dom.DocumentNode.SelectNodes("//a[@data-ds-appid]");

        if (matches == null || matches.Count == 0)
            return [];

        foreach (var match in matches)
        {
            try
            {
                var appId = match.Attributes["data-ds-appid"].Value;
                var matchNameElement = match.SelectSingleNode(".//div[@class = 'match_name']");

                appId = appId.Split(',').First();

                if (matchNameElement != null)
                {
                    results.Add(new GameSearchResult
                    {
                        Name = matchNameElement.InnerText,
                        AppId = Convert.ToInt32(appId)
                    });
                }
            }
            catch
            {
                // Ignore
            }
        }

        return results;
    }
    
    public static Uri GetWebAssetUri(int appId, WebAssetType type)
    {
        Dictionary<WebAssetType, string> webAssetTypeMap = new Dictionary<WebAssetType, string>()
        {
            { WebAssetType.Capsule, "capsule_231x87.jpg" },
            { WebAssetType.CapsuleLarge, "capsule_616x353.jpg" },
            { WebAssetType.Header, "header.jpg" },
            { WebAssetType.HeroCapsule, "hero_capsule.jpg" },
            { WebAssetType.LibraryCover, "library_600x900.jpg" },
            { WebAssetType.LibraryHeader, "library_header.jpg" },
            { WebAssetType.LibraryHero, "library_hero.jpg" },
            { WebAssetType.Logo, "logo.png" }
        };

        return new Uri($"https://shared.cloudflare.steamstatic.com/store_item_assets/steam/apps/{appId}/{webAssetTypeMap[type]}");
    }
}