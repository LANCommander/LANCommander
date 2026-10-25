using LANCommander.SDK.Enums;

namespace LANCommander.Server.Plugins;

/// <summary>An image, video or other file shown for a game, such as its cover or icon.</summary>
public sealed record MediaInfo
{
    public Guid Id { get; init; }
    public string? Name { get; init; }
    public MediaType Type { get; init; }

    /// <summary>Where the file came from, if anywhere.</summary>
    public string? SourceUrl { get; init; }

    public string? MimeType { get; init; }
    public int SortOrder { get; init; }

    /// <summary>The file's checksum. Set by the server; ignored when writing.</summary>
    public string? Crc32 { get; init; }

    /// <summary>Set by the server; ignored when writing.</summary>
    public Guid StorageLocationId { get; init; }

    /// <summary>Set by the server; ignored when writing.</summary>
    public Guid GameId { get; init; }
}

/// <summary>
/// Manages the media of games. Provided by the server; inject it into a scoped plugin service or
/// page. New files are stored in the server's default media storage location.
/// </summary>
public interface IMediaProvider
{
    /// <summary>A game's media, ordered by type and sort order.</summary>
    /// <exception cref="KeyNotFoundException">There is no such game.</exception>
    Task<IReadOnlyList<MediaInfo>> GetAsync(Guid gameId, CancellationToken cancellationToken = default);

    /// <summary>The media with the given id, or null.</summary>
    Task<MediaInfo?> GetMediaAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Adds media to a game from a stream, which is read to the end.</summary>
    /// <exception cref="KeyNotFoundException">There is no such game.</exception>
    Task<MediaInfo> AddAsync(Guid gameId, MediaInfo media, Stream file, CancellationToken cancellationToken = default);

    /// <summary>Adds media to a game, downloading the file from a URL.</summary>
    /// <exception cref="KeyNotFoundException">There is no such game.</exception>
    Task<MediaInfo> AddFromUrlAsync(Guid gameId, MediaInfo media, string url, CancellationToken cancellationToken = default);

    /// <summary>Writes the name, type, source URL, MIME type and sort order of media. The file is untouched.</summary>
    /// <exception cref="KeyNotFoundException">There is no such media.</exception>
    Task<MediaInfo> UpdateAsync(MediaInfo media, CancellationToken cancellationToken = default);

    /// <summary>Replaces the file of existing media.</summary>
    /// <exception cref="KeyNotFoundException">There is no such media.</exception>
    Task<MediaInfo> ReplaceFileAsync(Guid id, Stream file, CancellationToken cancellationToken = default);

    /// <summary>Opens the media's file for reading, or returns null when there is no such media or file.</summary>
    Task<Stream?> OpenReadAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Deletes media and its file. Does nothing when there is no such media.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
