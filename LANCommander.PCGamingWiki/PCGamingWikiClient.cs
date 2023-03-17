using HtmlAgilityPack;
using LANCommander.SDK;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace LANCommander.PCGamingWiki
{
    public class PCGamingWikiClient
    {
        private HttpClient Client;

        public PCGamingWikiClient()
        {
            Client = new HttpClient();
            Client.BaseAddress = new Uri("https://www.pcgamingwiki.com/");
        }

        public async Task<Uri> Search(string keyword)
        {
            var results = await Client.GetStringAsync($"w/api.php?action=opensearch&format=json&formatversion=2&search={HttpUtility.UrlEncode(keyword)}&limit=1");

            string pattern = @"(https:\/\/www.pcgamingwiki.com.+)""";
            RegexOptions options = RegexOptions.Multiline;

            var match = Regex.Match(results, pattern, options);

            if (match.Success && match.Groups.Count == 2)
            {
                return new Uri(match.Groups[1].Value);
            }

            return null;
        }

        public async Task<Dictionary<string, int>> GetMultiplayerPlayerCounts(string keyword)
        {
            var url = await Search(keyword);

            return await GetMultiplayerPlayerCounts(url);
        }

        public async Task<Dictionary<string, int>> GetMultiplayerPlayerCounts(Uri url)
        {
            var results = new Dictionary<string, int>();

            if (url == null)
                return results;

            HtmlWeb web = new HtmlWeb();
            HtmlDocument dom = web.Load(url);

            HtmlNode multiplayerTable = dom.GetElementbyId("table-network-multiplayer");

            if (multiplayerTable == null)
                return null;

            var multiplayerRows = multiplayerTable.SelectNodes(".//tr[contains(@class, 'table-network-multiplayer-body-row')]");
            var multiplayerAbbrs = multiplayerTable.SelectNodes(".//abbr");
            var multiplayerCounts = multiplayerTable.SelectNodes(".//td[contains(@class, 'table-network-multiplayer-body-players')]");

            foreach (var row in multiplayerRows)
            {
                var abbr = row.SelectNodes(".//abbr");
                var count = row.SelectNodes(".//td[contains(@class, 'table-network-multiplayer-body-players')]");

                if (abbr == null || count == null)
                    continue;

                var type = abbr[0].InnerText;
                var players = count[0].InnerText;

                int playerCount = 0;

                if (Int32.TryParse(players, out playerCount))
                {
                    switch (type.ToLower())
                    {
                        case "local play":
                            results["Local Play"] = playerCount;
                            break;

                        case "lan play":
                            results["LAN Play"] = playerCount;
                            break;

                        case "online play":
                            results["Online Play"] = playerCount;
                            break;
                    }
                }
            }

            return results;
        }
    }
}
