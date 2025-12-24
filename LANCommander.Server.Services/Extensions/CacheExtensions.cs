using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services.Extensions;

public static class CacheExtensions
{
    private static string GetThreadParticipantCacheKey(Guid threadId) => $"Chat/Thread/{threadId}/Participants";
    
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

    public static async Task<List<string>> GetChatThreadParticipants(this IFusionCache cache, Guid threadId)
    {
        var participants = await cache.TryGetAsync<List<string>>(GetThreadParticipantCacheKey(threadId));
        
        return participants.HasValue ? participants.Value : [];
    }

    public static async Task SetChatThreadParticipants(this IFusionCache cache, Guid threadId,
        List<string> participants) 
        => await cache.SetAsync(GetThreadParticipantCacheKey(threadId), participants);
}