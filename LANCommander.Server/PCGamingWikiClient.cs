using HtmlAgilityPack;
using LANCommander.SDK;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace LANCommander.PCGamingWiki
{
    public class PCGamingWikiClient
    {
        #region Internal models

        public class GetPageIdModel
        {
            public class Cargoquery
            {
                public class Title
                {
                    public string PageID { get; set; }
                    public string Page { get; set; }

                    [JsonProperty("Steam AppID")]
                    public string SteamAppID { get; set; }
                }
                public Title title { get; set; }
            }

            public List<Cargoquery> cargoquery { get; } = new List<Cargoquery>();
        }

        public class GetPageContentModel
        {
            public class Parse
            {
                public class Text
                {
                    [JsonProperty("*")]
                    public string content { get; set; }
                }

                public string title { get; set; }
                public int pageid { get; set; }
                public Text text { get; set; }
            }
            public Parse parse { get; set; }
        }
        
        #endregion

        private HttpClient Client;

        public PCGamingWikiClient()
        {
            Client = new HttpClient();
            Client.BaseAddress = new Uri("https://www.pcgamingwiki.com/");
            Client.DefaultRequestHeaders.UserAgent.ParseAdd($"{AssemblyName.GetAssemblyName(Assembly.GetExecutingAssembly().Location).Name}/{Assembly.GetExecutingAssembly().GetName().Version.ToString()}");
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
            var pageId = await GetPageIDAsync(keyword);
            if (pageId != null)
            {
                var pageContent = await GetPageContentAsync(pageId);

                var dom = new HtmlDocument();
                dom.LoadHtml(pageContent ?? string.Empty);
                return GetMultiplayerPlayerCounts(dom);
            }

            return [];
        }

        public Dictionary<string, int> GetMultiplayerPlayerCounts(Uri url)
        {
            if (url == null)
                return [];

            HtmlWeb web = new();
            HtmlDocument dom = web.Load(url);
            return GetMultiplayerPlayerCounts(dom);
        }

        protected Dictionary<string, int> GetMultiplayerPlayerCounts(HtmlDocument dom)
        {
            ArgumentNullException.ThrowIfNull(dom);

            var results = new Dictionary<string, int>();

            HtmlNode multiplayerTable = dom.GetElementbyId("table-network-multiplayer");
            if (multiplayerTable == null)
                return results;

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

        protected async Task<string?> GetPageIDAsync(string keyword)
        {
            try
            {
                string getPageIdUrl = $"https://www.pcgamingwiki.com/w/api.php?action=cargoquery&format=json&tables=Infobox_game&fields=Infobox_game._pageID%3DPageID%2C_pageName%3DPage%2CSteam_AppID&where=Infobox_game._pageName%3D%22{HttpUtility.UrlEncode(keyword)}%22&formatversion=1";

                // HttpClient.GetFromJsonAsync<> does not seem to deserialize list properly (at least with the usedmodel)
                // read json and parse via Newtonsoft lib
                string getPageIdJson = await Client.GetStringAsync(getPageIdUrl);
                var pageId = JsonConvert.DeserializeObject<GetPageIdModel>(getPageIdJson);

                if (pageId == null || pageId.cargoquery == null || pageId.cargoquery.Count != 1)
                    return null;

                var title = pageId.cargoquery[0]?.title;
                return title?.PageID;
            }
            catch (Exception ex)
            {
                // TODO: add logging
                //Logger?.LogError(ex, $"Could not parse page id from PCGamingWiki for retrieving page with keyword {keyword}");
            }

            return null;
        }

        protected async Task<string?> GetPageContentAsync(string pageId)
        {
            try
            {
                string getPageContentUrl = $"https://www.pcgamingwiki.com/w/api.php?action=parse&format=json&prop=text&pageid={HttpUtility.UrlEncode(pageId)}";

                // HttpClient.GetFromJsonAsync<> does not seem to deserialize list properly (at least with the usedmodel)
                // read json and parse via Newtonsoft lib
                string getPageContentJson = await Client.GetStringAsync(getPageContentUrl);
                var pageContent = JsonConvert.DeserializeObject<GetPageContentModel>(getPageContentJson);
                return pageContent?.parse?.text?.content;
            }
            catch (Exception ex)
            {
                // TODO: add logging
                //Logger?.LogError(ex, $"Could not parse page content from PCGamingWiki for retrieving page with id {pageId}");
            }

            return null;
        }
    }
}
