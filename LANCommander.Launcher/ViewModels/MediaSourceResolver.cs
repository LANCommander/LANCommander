using System;
using System.IO;
using LANCommander.SDK.Services;

namespace LANCommander.Launcher.ViewModels;

/// <summary>
/// Resolves an image source for a piece of media, preferring the locally cached file written by
/// the background import and falling back to streaming it straight from the server when it hasn't
/// been cached yet. This lets the library and detail views render instantly while the import is
/// still running, then transparently switch to the on-disk copy once it lands.
/// </summary>
public static class MediaSourceResolver
{
    /// <summary>
    /// Returns a local file path when the media is already cached, otherwise a server URL:
    /// the range-capable stream endpoint for video, or the resized thumbnail for images.
    /// Returns null when there is no media.
    /// </summary>
    public static string? Resolve(SDK.Models.Media? media, MediaClient mediaClient)
    {
        if (media == null)
            return null;

        var localPath = mediaClient.GetLocalPath(media);

        if (File.Exists(localPath))
            return localPath;

        if (media.MimeType?.StartsWith("video/", StringComparison.OrdinalIgnoreCase) == true)
            return mediaClient.GetAbsoluteStreamUrl(media);

        return mediaClient.GetAbsoluteThumbnailUrl(media);
    }
}
