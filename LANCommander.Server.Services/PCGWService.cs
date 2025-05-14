using IGDB;
using IGDB.Models;
using Microsoft.Extensions.Logging;
using System.Text;
using LANCommander.SDK.Enums;
using LANCommander.Server.Services.Models;
using Company = LANCommander.Server.Data.Models.Company;
using LANCommander.PCGamingWiki;
using Microsoft.AspNetCore.Http;
using Docker.DotNet.Models;

namespace LANCommander.Server.Services
{
    public class PCGWService(
        ILogger<PCGWService> logger) : BaseService(logger), IDisposable
    {
        private const string DefaultFields = "*";
        private PCGamingWikiClient Client;

        public override void Initialize()
        {
            Client = new PCGamingWikiClient();
        }

        public async Task<IEnumerable<SearchGamesResult>> SearchGamesAsync(string input, int limit = 10, int offset = 0)
        {
            var games = await Client.SearchGamesAsync(input, limit, offset);
            return games.AsEnumerable();
        }

        public async Task<IEnumerable<PCGWMultiplayerModeLookupResult>> SearchGameForMultiplayerPlayerInfoAsync(string keyword)
        {
            var modes = await Client.GetMultiplayerPlayerInfo(keyword);
            return BuildAndFilterResults(modes);
        }

        public async Task<IEnumerable<PCGWMultiplayerModeLookupResult>> GetMultiplayerPlayerInfoAsync(int pageId)
        {
            var modes = await Client.GetMultiplayerPlayerInfo(pageId);
            return BuildAndFilterResults(modes);
        }

        public static string GetThumbnailUrlOfImageUrl(string coverUrl)
        {
            if (Uri.TryCreate(coverUrl, UriKind.Absolute, out Uri uri))
            {
                var fileName = uri.Segments.LastOrDefault() ?? "";
                var newHost = uri.Host.Replace("images.", "thumbnails.", StringComparison.OrdinalIgnoreCase);

                string hostPort = uri.IsDefaultPort ? "" : $":{uri.Port}";
                return $"{uri.Scheme}://{newHost}{hostPort}{uri.AbsolutePath}/100px-{fileName}{uri.Query}{uri.Fragment}";
            }

            return null;
        }

        public MultiplayerType? GetMultiplayerType(string multiplayerType)
        {
            switch (multiplayerType.ToLower())
            {
                case "local":
                case "local play":
                    return MultiplayerType.Local;

                case "lan":
                case "lan play":
                    return MultiplayerType.LAN;

                case "online":
                case "online play":
                    return MultiplayerType.Online;
            }

            return null;
        }

        private IEnumerable<PCGWMultiplayerModeLookupResult> BuildAndFilterResults(IEnumerable<MultiplayerInfo> modes)
        {
            return modes.Select(mode =>
            {
                var type = GetMultiplayerType(mode.MultiplayerType);
                if (!type.HasValue) return new PCGWMultiplayerModeLookupResult[0];

                return [new PCGWMultiplayerModeLookupResult
                {
                    MultiplayerType = type.GetValueOrDefault(default),
                    PlayerCount = mode.PlayerCount,
                    Notes = mode.Notes,
                }];
            })
            .SelectMany(x => x)
            .AsEnumerable();
        }

        public void Dispose()
        {

        }
    }
}
