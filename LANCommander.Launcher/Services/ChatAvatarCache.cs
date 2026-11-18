using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using LANCommander.SDK;
using LANCommander.SDK.Services;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services;

/// <summary>
/// Other users' avatars as local files, for the places that can only take a path, like a desktop
/// notification. Fetched from the server's anonymous <c>/api/Profile/{userName}/Avatar</c> and kept
/// for a while, so a busy conversation doesn't download the same picture for every message.
/// </summary>
public sealed class ChatAvatarCache(ProfileClient profileClient, ILogger<ChatAvatarCache> logger)
{
    static readonly TimeSpan MaxAge = TimeSpan.FromHours(1);

    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    // One download per user at a time, however many messages arrive at once
    readonly ConcurrentDictionary<string, Task<string?>> _pending = new(StringComparer.OrdinalIgnoreCase);

    /// <returns>The avatar's path, or null when the user has none or it couldn't be fetched</returns>
    public async Task<string?> GetPathAsync(string? userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
            return null;

        var cached = Find(userName);

        if (cached != null && DateTime.UtcNow - File.GetLastWriteTimeUtc(cached) < MaxAge)
            return cached;

        var download = _pending.GetOrAdd(userName, name => DownloadAsync(name, cached));

        try
        {
            return await download;
        }
        finally
        {
            _pending.TryRemove(userName, out _);
        }
    }

    async Task<string?> DownloadAsync(string userName, string? cached)
    {
        try
        {
            using var response = await Http.GetAsync(profileClient.GetAvatarUri(userName));

            // No avatar (404), or one in a format notifications can't show
            if (!response.IsSuccessStatusCode
                || Extension(response.Content.Headers.ContentType?.MediaType) is not { } extension)
                return cached;

            var path = BasePath(userName) + extension;

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            // Written beside and swapped in, so a reader never sees half a picture
            var temporary = path + ".tmp";

            await using (var file = File.Create(temporary))
                await response.Content.CopyToAsync(file);

            File.Move(temporary, path, overwrite: true);

            // The same user's picture saved in another format before is stale now
            if (cached != null && cached != path)
                File.Delete(cached);

            return path;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not fetch the avatar for {UserName}", userName);

            // A stale picture is better than none
            return cached;
        }
    }

    // Windows toasts pick images by extension and take only these
    static readonly string[] Extensions = [".png", ".jpg", ".gif"];

    static string? Extension(string? mediaType) => mediaType?.ToLowerInvariant() switch
    {
        "image/png" => ".png",
        "image/jpeg" or "image/jpg" => ".jpg",
        "image/gif" => ".gif",
        _ => null,
    };

    static string? Find(string userName)
    {
        var basePath = BasePath(userName);

        foreach (var extension in Extensions)
        {
            if (File.Exists(basePath + extension))
                return basePath + extension;
        }

        return null;
    }

    static string BasePath(string userName)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
            userName = userName.Replace(invalid, '_');

        return Path.Combine(AppPaths.GetConfigDirectory(), "Cache", "Avatars", userName);
    }
}
