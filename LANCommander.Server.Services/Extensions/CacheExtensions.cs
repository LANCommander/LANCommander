using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services.Extensions;

public static class CacheExtensions
{
    public static async Task ExpireGameCacheAsync(this IFusionCache cache)
    {
        await cache.RemoveByTagAsync(["Games", "Depot"]);
    }
    public static async Task ExpireGameCacheAsync(this IFusionCache cache, Guid? gameId)
    {
        if (gameId.HasValue)
            await cache.RemoveByTagAsync([ 
                $"Games/{gameId}",
                "Games", 
                "Depot",
                $"Games/{gameId}/Archives",
            ]);
        else
            await cache.RemoveByTagAsync(["Games", "Depot"]);
    }

    public static Task ExpireArchiveCacheAsync(this IFusionCache cache)
    {
        return ExpireArchiveCacheAsync(cache, archiveId: null);
    }

    public static async Task ExpireArchiveCacheAsync(this IFusionCache cache, Guid? archiveId)
    {
        if (archiveId.HasValue)
            await cache.RemoveByTagAsync([$"Archives/{archiveId}"]);
        else
            await cache.RemoveByTagAsync(["Archives"]);
    }
}