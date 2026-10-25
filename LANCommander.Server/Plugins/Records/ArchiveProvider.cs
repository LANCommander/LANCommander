using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.Plugins.Records;

/// <summary>Gives plugins the server's archives without exposing the database model.</summary>
internal sealed class ArchiveProvider(ArchiveService archives) : IArchiveProvider
{
    public async Task<IReadOnlyList<ArchiveInfo>> GetAsync(RecordOwner owner, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        RecordGuard.OwnerIs(owner, "Archives", RecordOwnerKind.Game, RecordOwnerKind.Redistributable, RecordOwnerKind.Tool);

        var id = owner.Id;
        var versionId = owner.VersionId;

        var found = owner.Kind switch
        {
            RecordOwnerKind.Game when versionId != null => await archives.GetAsync(a => a.GameId == id && a.GameVersionId == versionId),
            RecordOwnerKind.Game => await archives.GetAsync(a => a.GameId == id),
            RecordOwnerKind.Redistributable => await archives.GetAsync(a => a.RedistributableId == id),
            _ => await archives.GetAsync(a => a.ToolId == id),
        };

        return found.OrderByDescending(a => a.CreatedOn).Select(Info).ToList();
    }

    public async Task<ArchiveInfo?> GetArchiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var archive = await archives.GetAsync(id);

        return archive == null ? null : Info(archive);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var archive = await archives.GetAsync(id);

        if (archive != null)
            await archives.DeleteAsync(archive);
    }

    static ArchiveInfo Info(Archive archive) => new()
    {
        Id = archive.Id,
        Version = archive.Version,
        StorageLocationId = archive.StorageLocationId,
        GameId = archive.GameId,
        GameVersionId = archive.GameVersionId,
        RedistributableId = archive.RedistributableId,
        ToolId = archive.ToolId,
        CompressedSize = archive.CompressedSize,
        UncompressedSize = archive.UncompressedSize,
        CreatedOn = archive.CreatedOn,
    };
}
