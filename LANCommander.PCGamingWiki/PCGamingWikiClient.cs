using HtmlAgilityPack;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
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
        
        public class SearchGameModel
        {
            public class Cargoquery
            {
                public class Title
                {
                    public string PageID { get; set; }
                    public string Page { get; set; }
                    public string Released { get; set; }
                    public string Developers { get; set; }
                    public string Publishers { get; set; }

                    [JsonProperty("Cover URL")]
                    public string CoverURL { get; set; }

                    [JsonProperty("Steam AppID")]
                    public string SteamAppID { get; set; }
                    public string Released__precision { get; set; }
                }

                public Title title { get; set; }
            }

            public List<Cargoquery> cargoquery { get; set; }
        }

        #endregion

        private HttpClient Client;

        public PCGamingWikiClient()
        {
            Client = new HttpClient();
            Client.BaseAddress = new Uri("https://www.pcgamingwiki.com/w/");
            Client.DefaultRequestHeaders.UserAgent.ParseAdd($"{AssemblyName.GetAssemblyName(Assembly.GetExecutingAssembly().Location).Name}/{Assembly.GetExecutingAssembly().GetName().Version.ToString()}");
        }

        public async Task<IEnumerable<SearchGamesResult>> SearchGamesAsync(string keyword, int limit = 10, int offset = 0)
        {
            try
            {
                string searchUrl = $"api.php?action=cargoquery&format=json&limit={limit}&offset={offset}&tables=Infobox_game&fields=Infobox_game._pageID%3DPageID%2C_pageName%3DPage%2CReleased%2CDevelopers%2CPublishers%2CCover_URL%2CSteam_AppID&where=Infobox_game._pageName%20LIKE%20%22%25{HttpUtility.UrlEncode(keyword)}%25%22";
                string searchJson = await Client.GetStringAsync(searchUrl);
                var searchResult = JsonConvert.DeserializeObject<SearchGameModel>(searchJson);

                var results = searchResult?.cargoquery;
                if (results?.Count > 0)
                {
                    return results.Select(r => r.title).Select(game => new SearchGamesResult
                    {
                        PageID = TryParseInt(game.PageID, 0),
                        PageKey = game.PageID,
                        PageName = game.Page,
                        Released = game.Released?.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => TryParseDateTime(s, null)).Where(x => x.HasValue).Cast<DateTime>().ToArray() ?? [],
                        Developers = game.Developers?.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => RemoveFirst(s.Trim(), "Company:")).ToArray() ?? [],
                        Publishers = game.Publishers?.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => RemoveFirst(s.Trim(), "Company:")).ToArray() ?? [],
                        CoverURL = game.CoverURL,
                        SteamAppIds = game.SteamAppID?.Split('m').Select(s => TryParseUInt(s, 0)).ToArray() ?? [],
                    }).ToArray();
                }
            }
            catch (Exception ex)
            {
                // TODO: add logging
                //Logger?.LogError(ex, $"Could not search PCGamingWiki for keyword {keyword}");
            }

            return [];

            static string RemoveFirst(string source, string remove)
            {
                int index = source.IndexOf(remove);
                return (index < 0) ? source : source.Remove(index, remove.Length);
            }

            static DateTime? TryParseDateTime(string s, DateTime? defaultValue)
            {
                return DateTime.TryParse(s, out DateTime newValue) ? newValue : defaultValue;
            }
            static uint TryParseUInt(string s, uint defaultValue)
            {
                return uint.TryParse(s, out uint newValue) ? newValue : defaultValue;
            }
            static int TryParseInt(string s, int defaultValue)
            {
                return int.TryParse(s, out int newValue) ? newValue : defaultValue;
            }
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

        public Dictionary<string, int?> GetMultiplayerPlayerCounts(Uri url)
        {
            if (url == null)
                return [];

            HtmlWeb web = new();
            HtmlDocument dom = web.Load(url);
            return GetMultiplayerPlayerCounts(dom);
        }

        public async Task<Dictionary<string, int?>> GetMultiplayerPlayerCounts(string keyword)
        {
            var data = await SearchAndParseMultiplayerPlayerData(keyword, info => info.ToDictionary(x => x.Key, y => y.Value.PlayerCount));
            return data ?? [];
        }

        public async Task<IEnumerable<MultiplayerInfo>> GetMultiplayerPlayerInfo(string keyword)
        {
            var data = await SearchAndParseMultiplayerPlayerData(keyword, info => info.Values.ToArray());
            return data ?? [];
        }

        public async Task<IEnumerable<MultiplayerInfo>> GetMultiplayerPlayerInfo(int pageId)
        {
            var data = await SearchAndParseMultiplayerPlayerData(pageId, info => info.Values.ToArray());
            return data ?? [];
        }

        protected async Task<T?> SearchAndParseMultiplayerPlayerData<T>(string keyword, Func<Dictionary<string, MultiplayerInfo>, T> dataPropagateFunction) where T : class
        {
            var pageId = await GetPageIDAsync(keyword);
            if (pageId != null)
            {
                return await SearchAndParseMultiplayerPlayerData(pageId.Value, dataPropagateFunction);
            }

            return null;
        }

        protected async Task<T?> SearchAndParseMultiplayerPlayerData<T>(int pageId, Func<Dictionary<string, MultiplayerInfo>, T> dataPropagateFunction) where T : class
        {
            var pageContent = await GetPageContentAsync(pageId);

            var dom = new HtmlDocument();
            dom.LoadHtml(pageContent ?? string.Empty);
            var data = ParseMultiplayerPlayerInfo(dom);
            return dataPropagateFunction.Invoke(data);
        }

        protected Dictionary<string, int?> GetMultiplayerPlayerCounts(HtmlDocument dom)
        {
            return ParseMultiplayerPlayerInfo(dom).ToDictionary(x => x.Key, y => y.Value.PlayerCount);
        }

        protected Dictionary<string, MultiplayerInfo> ParseMultiplayerPlayerInfo(HtmlDocument dom)
        {
            ArgumentNullException.ThrowIfNull(dom);

            HtmlNode multiplayerTable = dom.GetElementbyId("table-network-multiplayer");

            if (multiplayerTable == null)
                return [];

            var results = new Dictionary<string, MultiplayerInfo>(StringComparer.OrdinalIgnoreCase);

            var multiplayerRows = multiplayerTable.SelectNodes(".//tr[contains(@class, 'table-network-multiplayer-body-row')]");
            var multiplayerAbbrs = multiplayerTable.SelectNodes(".//abbr");
            var multiplayerCounts = multiplayerTable.SelectNodes(".//td[contains(@class, 'table-network-multiplayer-body-players')]");
            var multiplayerNotes = multiplayerTable.SelectNodes(".//td[contains(@class, 'table-network-multiplayer-body-notes')]");

            foreach (var row in multiplayerRows)
            {
                var abbr_node = row.SelectNodes(".//abbr");
                var rating_node = row.SelectNodes(".//td[contains(@class, 'table-network-multiplayer-body-rating')]");
                var count_node = row.SelectNodes(".//td[contains(@class, 'table-network-multiplayer-body-players')]");
                var notes_node = row.SelectNodes(".//td[contains(@class, 'table-network-multiplayer-body-notes')]");

                if (abbr_node == null || (count_node == null && rating_node == null))
                    continue;

                var type = abbr_node[0].InnerText;
                if (type == null)
                    continue;

                var players = count_node?.FirstOrDefault()?.InnerText;
                var notes = null as string;
                if (notes_node.Count == 1 && notes_node[0]?.ChildNodes.Count > 0)
                {
                    var notelines = notes_node.FirstOrDefault()?.ChildNodes
                        .Select(x => x.InnerText)
                        .Select(x => x.TrimEnd('.'));
                    notes = string.Join("\n", notelines ?? []);
                    notes = notes.Replace("\n\n", ". ");
                    notes = notes.ReplaceLineEndings("");
                    notes = HttpUtility.HtmlDecode(notes);
                }

                var rating = null as string;
                if (rating_node?.Count > 0)
                {
                    var rating_title = rating_node?.FirstOrDefault()?.FirstChild;
                    rating = rating_title?.Attributes["title"]?.Value;

                    // skip multiplayer types when "native" support is not given
                    if (rating_title != null && (rating_title.HasClass("tickcross-false") || string.Equals(rating_title.InnerHtml, "No native support", StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    string rating_note = (!rating_title?.HasClass("tickcross-true") ?? false) ? $"{rating}. " : "";
                    notes = (rating_note ?? "") + (notes ?? "");
                }

                results.TryAdd(type, new MultiplayerInfo { MultiplayerType = type });

                if (Int32.TryParse(players, out int playerCount))
                {
                    results[type].PlayerCount = playerCount;
                }
                if (!string.IsNullOrEmpty(notes))
                {
                    results[type].Notes = notes ?? "";
                }
            }

            return results;
        }

        protected async Task<int?> GetPageIDAsync(string keyword)
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
                if (int.TryParse(title?.PageID ?? "", out int pageIdInt))
                {
                    return pageIdInt;
                }
            }
            catch (Exception ex)
            {
                // TODO: add logging
                //Logger?.LogError(ex, $"Could not parse page id from PCGamingWiki for retrieving page with keyword {keyword}");
            }

            return null;
        }

        protected async Task<string?> GetPageContentAsync(int pageId)
        {
            try
            {
                string getPageContentUrl = $"https://www.pcgamingwiki.com/w/api.php?action=parse&format=json&prop=text&pageid={pageId}";

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

    public record MultiplayerInfo()
    {
        public required string MultiplayerType { get; set; }
        public int? PlayerCount { get; set; }
        public string? Notes { get; set; }
    }
}
