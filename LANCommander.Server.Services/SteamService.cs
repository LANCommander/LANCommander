using IGDB;
using IGDB.Models;
using Microsoft.Extensions.Logging;
using System.Text;
using LANCommander.SDK.Enums;
using LANCommander.Server.Services.Models;
using Company = LANCommander.Server.Data.Models.Company;
using LANCommander.Steam;
using Steam.Models.SteamStore;
using System.Security.Policy;
using System;
using System.Net.Mime;

namespace LANCommander.Server.Services
{
    public class SteamService(
        ILogger<IGDBService> logger) : BaseService(logger), IDisposable
    {
        private SteamClient Client;

        public override void Initialize()
        {
            Client = new SteamClient();
        }

        public async Task<IEnumerable<SteamGameLookupResult>> SearchGameAsync(string input)
        {
            var games = await Client.SearchGamesAsync(input);

            return games.Select(x =>
            {
                return new SteamGameLookupResult
                {
                    Name = x.Name,
                    AppId = x.AppId,
                    ImageUrl = x.ImageUrl,
                };
            });
        }

        public async Task<IEnumerable<SteamIconLookupResult>> SearchIconsAsync(string input)
        {
            var icons = await Client.SearchIconsAsync(input);

            return icons.Select(x =>
            {
                return new SteamIconLookupResult
                {
                    Name = x.Name,
                    AppId = x.AppId,
                    LogoUrl = x.Logo,
                    IconUrl = x.Icon,
                };
            });
        }

        public async Task<SteamGameManualLookupResult> GetWebManualAsync(int appId)
        {
            bool hasManual = await Client.HasManualAsync(appId);
            if (!hasManual)
                return null;

            var url = SteamClient.GetManualUri(appId);
            return new SteamGameManualLookupResult()
            {
                AppId = appId,
                ManualUrl = url.ToString(),
                PreviewUrl = "/static/pdf.png",
                MimeType = MediaTypeNames.Application.Pdf,
            };
        }

        public async Task<IEnumerable<SteamGameAssetLookupResult>> GetWebAssetsAsync(int appId)
        {
            var result = new List<SteamGameAssetLookupResult>();

            foreach (WebAssetType type in Enum.GetValues<WebAssetType>())
            {
                var existsResult = await Client.HasWebAssetAsync(appId, type);
                if (!existsResult.Exists)
                    continue;

                var url = SteamClient.GetWebAssetUri(appId, type);
                result.Add(new SteamGameAssetLookupResult()
                {
                    AppId = appId,
                    AssetType = type,
                    AssetUrl = url.ToString(),
                    MimeType = existsResult.MimeType,
                });
            }

            return result;
        }

        public void Dispose()
        {
            Client?.Dispose();
        }
    }
}
