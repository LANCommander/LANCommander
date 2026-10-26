using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.Plugins.Records;

/// <summary>Gives plugins the media of games without exposing the database model.</summary>
internal sealed class MediaProvider(
    MediaService media,
    GameService games,
    IHttpClientFactory httpClientFactory) : IMediaProvider
{
    public async Task<IReadOnlyList<MediaInfo>> GetAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await RequireGameAsync(gameId);

        return (await media.GetAsync(m => m.GameId == gameId))
            .OrderBy(m => m.Type)
            .ThenBy(m => m.SortOrder)
            .Select(Info)
            .ToList();
    }

    public async Task<MediaInfo?> GetMediaAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var found = await media.GetAsync(id);

        return found == null ? null : Info(found);
    }

    public async Task<MediaInfo> AddAsync(Guid gameId, MediaInfo info, Stream file, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await RequireGameAsync(gameId);

        return Info(await media.WriteToFileAsync(await NewAsync(gameId, info), file));
    }

    public async Task<MediaInfo> AddFromUrlAsync(Guid gameId, MediaInfo info, string url, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new ArgumentException("Media can only be downloaded over HTTP or HTTPS.", nameof(url));

        await RequireGameAsync(gameId);

        using var http = httpClientFactory.CreateClient();
        using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        var entity = await NewAsync(gameId, info with
        {
            SourceUrl = string.IsNullOrWhiteSpace(info.SourceUrl) ? url : info.SourceUrl,
            MimeType = string.IsNullOrWhiteSpace(info.MimeType) ? response.Content.Headers.ContentType?.MediaType : info.MimeType,
        });

        return Info(await media.WriteToFileAsync(entity, stream));
    }

    public async Task<MediaInfo> UpdateAsync(MediaInfo info, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entity = RecordGuard.Found(await media.Include(m => m.CreatedBy!).GetAsync(info.Id), "media", info.Id);

        Apply(info, entity);

        await media.UpdateAsync(entity);

        return Info(RecordGuard.Found(await media.GetAsync(info.Id), "media", info.Id));
    }

    public async Task<MediaInfo> ReplaceFileAsync(Guid id, Stream file, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entity = RecordGuard.Found(await media.Include(m => m.StorageLocation, m => m.CreatedBy!).GetAsync(id), "media", id);

        return Info(await media.WriteToFileAsync(entity, file, overwrite: true));
    }

    public async Task<Stream?> OpenReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entity = await media.Include(m => m.StorageLocation).GetAsync(id);

        if (entity?.StorageLocation == null)
            return null;

        var path = MediaService.GetMediaPath(entity);

        return File.Exists(path) ? File.OpenRead(path) : null;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // The storage location says where the file is, which deleting it needs
        var entity = await media.Include(m => m.StorageLocation).GetAsync(id);

        if (entity != null)
            await media.DeleteAsync(entity);
    }

    async Task<Media> NewAsync(Guid gameId, MediaInfo info)
    {
        // No id: WriteToFileAsync adds media without one, and updates media with one
        var entity = new Media
        {
            GameId = gameId,
            Crc32 = string.Empty,
            StorageLocation = await media.GetDefaultStorageLocationAsync()
                              ?? throw new InvalidOperationException("The server has no default media storage location."),
        };

        entity.StorageLocationId = entity.StorageLocation.Id;

        Apply(info, entity);

        return entity;
    }

    async Task RequireGameAsync(Guid gameId)
    {
        if (!await games.ExistsAsync(gameId))
            throw RecordGuard.NotFound("game", gameId);
    }

    static void Apply(MediaInfo info, Media entity)
    {
        entity.Name = info.Name;
        entity.Type = info.Type;
        entity.SourceUrl = info.SourceUrl;
        entity.MimeType = info.MimeType;
        entity.SortOrder = info.SortOrder;
    }

    static MediaInfo Info(Media entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Type = entity.Type,
        SourceUrl = entity.SourceUrl,
        MimeType = entity.MimeType,
        SortOrder = entity.SortOrder,
        Crc32 = entity.Crc32,
        StorageLocationId = entity.StorageLocationId,
        GameId = entity.GameId ?? Guid.Empty,
    };
}
