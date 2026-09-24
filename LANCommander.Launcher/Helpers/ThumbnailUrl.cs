using System;

namespace LANCommander.Launcher.Helpers;

/// <summary>
/// The server's default media thumbnails are sized for a 1x display. This asks for one sized to
/// the device pixels an image will be decoded at instead; servers that predate sized thumbnails
/// ignore the query and return the default.
/// </summary>
public static class ThumbnailUrl
{
    /// <summary>
    /// Adds <paramref name="width"/> (or, failing that, <paramref name="height"/>) to a server
    /// <c>/api/Media/{id}/Thumbnail</c> URL. Any other source, including local paths, passes through untouched.
    /// </summary>
    public static string WithSize(string source, int width, int height)
    {
        if (width <= 0 && height <= 0)
            return source;

        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || !String.IsNullOrEmpty(uri.Query)
            || !uri.AbsolutePath.EndsWith("/Thumbnail", StringComparison.OrdinalIgnoreCase))
            return source;

        return width > 0 ? $"{source}?width={width}" : $"{source}?height={height}";
    }
}
