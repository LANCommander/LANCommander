using HtmlAgilityPack;
using LANCommander.Steam.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;

namespace LANCommander.Steam;

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
            catch (Exception ex) { }
        }

        return results;
    }

    public async Task<(bool Exists, string MimeType)> HasWebAssetAsync(int appId, WebAssetType webAssetType)
    {
        var webAssetUri = GetWebAssetUri(appId, webAssetType);
        var response = await HttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, webAssetUri));

        var exists = response.Content.Headers.ContentType.MediaType == MediaTypeNames.Image.Jpeg || response.Content.Headers.ContentType.MediaType == "image/png";

        return (exists, response.Content.Headers.ContentType.MediaType);
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

    public async Task<IEnumerable<SteamScreenshot>> GetScreenshotsAsync(int appId)
    {
        try
        {
            using var response = await HttpClient.GetAsync($"/api/appdetails?appids={appId}&filters=screenshots");
            response.EnsureSuccessStatusCode();

            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var root = document.RootElement.GetProperty(appId.ToString());

            if (!root.GetProperty("success").GetBoolean())
                return Enumerable.Empty<SteamScreenshot>();

            var data = root.GetProperty("data");
            if (!data.TryGetProperty("screenshots", out var screenshotsElement))
                return Enumerable.Empty<SteamScreenshot>();

            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
            return screenshotsElement.Deserialize<SteamScreenshot[]>(options) ?? Enumerable.Empty<SteamScreenshot>();
        }
        catch
        {
            return Enumerable.Empty<SteamScreenshot>();
        }
    }

    public async Task<IEnumerable<SteamMovie>> GetMoviesAsync(int appId)
    {
        try
        {
            using var response = await HttpClient.GetAsync($"/api/appdetails?appids={appId}&filters=movies");
            response.EnsureSuccessStatusCode();

            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var root = document.RootElement.GetProperty(appId.ToString());

            if (!root.GetProperty("success").GetBoolean())
                return Enumerable.Empty<SteamMovie>();

            var data = root.GetProperty("data");
            if (!data.TryGetProperty("movies", out var moviesElement))
                return Enumerable.Empty<SteamMovie>();

            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
            return moviesElement.Deserialize<SteamMovie[]>(options) ?? Enumerable.Empty<SteamMovie>();
        }
        catch
        {
            return Enumerable.Empty<SteamMovie>();
        }
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
