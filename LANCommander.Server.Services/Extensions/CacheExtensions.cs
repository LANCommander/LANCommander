using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services.Extensions;

public static class CacheExtensions
{
    public static async Task ExpireGameCacheAsync(this IFusionCache cache, Guid? gameId)
    {
        if (gameId.HasValue)
        {
            await cache.RemoveByTagAsync([ $"Games/{gameId}", "Games", "Depot"]);
        }
    }
}