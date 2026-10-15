using LANCommander.SDK.Enums;

namespace LANCommander.Server.Plugins;

/// <summary>
/// Imports LCX packages through the server's own import pipeline, the same one the import API and
/// the import dialog use. Provided by the server; inject it into a scoped plugin service or page.
/// </summary>
public interface IPackageImporter
{
    /// <summary>Imports every record in an LCX file the server can already read.</summary>
    /// <param name="packagePath">Path to the package on the server.</param>
    /// <param name="storageLocationId">
    /// Archive storage location to write archives into. Falls back to the default archive location.
    /// </param>
    /// <param name="manifestType">Expected manifest type. When null the type is read from the manifest.</param>
    /// <param name="cancellationToken">Cancels the import.</param>
    Task<PackageImportResult> ImportFileAsync(
        string packagePath,
        Guid? storageLocationId = null,
        ManifestType? manifestType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies an LCX package stream to the server's temporary storage, then imports every record in it.
    /// </summary>
    /// <param name="package">Readable LCX package stream.</param>
    /// <param name="storageLocationId">
    /// Archive storage location to write archives into. Falls back to the default archive location.
    /// </param>
    /// <param name="manifestType">Expected manifest type. When null the type is read from the manifest.</param>
    /// <param name="copyProgress">Receives the number of bytes copied to temporary storage.</param>
    /// <param name="cancellationToken">Cancels the import.</param>
    Task<PackageImportResult> ImportStreamAsync(
        Stream package,
        Guid? storageLocationId = null,
        ManifestType? manifestType = null,
        IProgress<long>? copyProgress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>The outcome of an import run by <see cref="IPackageImporter"/>.</summary>
/// <param name="RecordId">Id of the record the import created or updated.</param>
/// <param name="ManifestType">The kind of record the package held.</param>
/// <param name="ImportedCount">Number of records imported.</param>
public sealed record PackageImportResult(Guid RecordId, ManifestType ManifestType, int ImportedCount);
