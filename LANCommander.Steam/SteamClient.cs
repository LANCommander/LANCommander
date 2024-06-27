using HtmlAgilityPack;
using SteamWebAPI2.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Mime;
using System.Threading.Tasks;

namespace LANCommander.Steam
{
    public class SteamClient
    {
        private readonly HttpClient HttpClient;

        public SteamClient()
        {
            HttpClient = new HttpClient();
            HttpClient.BaseAddress = new Uri("https://store.steampowered.com");
        }

        public async Task<IEnumerable<GameSearchResult>> SearchGamesAsync(string keyword)
        {
            HtmlWeb web = new HtmlWeb();
            HtmlDocument dom = await web.LoadFromWebAsync($"https://store.steampowered.com/search/suggest?term={keyword}&f=games&cc=US");

            var results = new List<GameSearchResult>();
            var matches = dom.DocumentNode.SelectNodes("//a[@data-ds-appid]");

            if (matches == null || matches.Count == 0)
                return Enumerable.Empty<GameSearchResult>();

            foreach (var match in matches)
            {
                var appId = match.Attributes["data-ds-appid"].Value;
                var matchNameElement = match.SelectSingleNode(".//div[@class = 'match_name']");

                if (matchNameElement != null)
                {
                    results.Add(new GameSearchResult
                    {
                        Name = matchNameElement.InnerText,
                        AppId = Convert.ToInt32(appId)
                    });
                }
            }

            return results;
        }

        public async Task<bool> HasManualAsync(int appId)
        {
            var manualUri = GetManualUri(appId);
            var response = await HttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, manualUri));

            return response.Content.Headers.ContentType.MediaType == MediaTypeNames.Application.Pdf;
        }

        public async Task<byte[]> DownloadManualAsync(int appId)
        {
            var manualUri = GetManualUri(appId);
            var response = await HttpClient.GetAsync(manualUri);

            if (!response.IsSuccessStatusCode)
                return null;

            using (var ms = new MemoryStream())
            {
                await response.Content.CopyToAsync(ms);

                return ms.ToArray();
            }
        }

        public static Uri GetManualUri(int appId)
        {
            return new Uri($"https://store.steampowered.com/manual/{appId}");
        }
    }
}
