using LANCommander.SDK.Enums;

namespace LANCommander.Server.Plugins;

/// <summary>
/// Adds a source to the server's media search, alongside the built-in ones such as HQ and
/// SteamGridDB. Register an implementation in <c>IPlugin.ConfigureServices</c>.
/// </summary>
/// <remarks>
/// The extension only searches. When an administrator picks a result, the server downloads its
/// <see cref="MediaGrabberItem.SourceUrl"/> itself.
/// </remarks>
public interface IMediaGrabberExtension
{
    /// <summary>
    /// The name the source is shown and filtered by in the media search. Must be unique among the
    /// server's sources.
    /// </summary>
    string Name { get; }

    /// <summary>The kinds of media the source can find.</summary>
    IReadOnlyCollection<MediaType> SupportedMediaTypes { get; }

    /// <summary>
    /// Whether the source returns results in pages. When false, the server asks only for page 0.
    /// </summary>
    bool SupportsPaging => false;

    /// <summary>
    /// Searches for media of one kind. Only called for a type in <see cref="SupportedMediaTypes"/>.
    /// A failure is shown as the source's error without affecting the other sources.
    /// </summary>
    /// <param name="type">The kind of media to find.</param>
    /// <param name="keywords">What the administrator searched for, usually a game's title.</param>
    /// <param name="page">The page of results, starting at 0.</param>
    /// <param name="cancellationToken">Cancelled when the search is abandoned.</param>
    Task<IReadOnlyList<MediaGrabberItem>> SearchAsync(MediaType type, string keywords, int page, CancellationToken cancellationToken);
}

/// <summary>One result of an <see cref="IMediaGrabberExtension"/> search.</summary>
public sealed record MediaGrabberItem
{
    /// <summary>Identifies the result within the source.</summary>
    public required string Id { get; init; }

    /// <summary>The kind of media the result is.</summary>
    public required MediaType Type { get; init; }

    /// <summary>Where the server downloads the media from.</summary>
    public required string SourceUrl { get; init; }

    /// <summary>A preview to show in the search results; may be the same as <see cref="SourceUrl"/>.</summary>
    public string? ThumbnailUrl { get; init; }

    /// <summary>The MIME type of the media at <see cref="SourceUrl"/>.</summary>
    public required string MimeType { get; init; }

    /// <summary>Results with the same group are shown together, e.g. under the game they belong to.</summary>
    public string? Group { get; init; }

    /// <summary>The result's own title where the source has one, e.g. a video's name.</summary>
    public string? Name { get; init; }

    /// <summary>Running time, for videos.</summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>A video's frame height as people say it ("1080p"), where the source knows it.</summary>
    public string? Resolution { get; init; }
}
