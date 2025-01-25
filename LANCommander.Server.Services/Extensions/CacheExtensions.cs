using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services.Extensions;

public static class CacheExtensions
{
    public static async Task ExpireGameCacheAsync(this IFusionCache cache, Guid? gameId)
    {
        if (gameId.HasValue)
        {
            await cache.ExpireAsync($"/Depot/Results");
            await cache.ExpireAsync($"Depot/Games/{gameId}");
            await cache.ExpireAsync($"Games");
            await cache.ExpireAsync($"Games/{gameId}");
            await cache.ExpireAsync($"Games/{gameId}/Manifest");
            await cache.ExpireAsync($"Games/{gameId}/Actions");
        }
    }
}