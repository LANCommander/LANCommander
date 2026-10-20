using LANCommander.SDK.Enums;
using LANCommander.Server.ImportExport.Factories;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.ImportExport.Services;

/// <summary>
/// Runs a complete import of an uploaded archive.
/// <para>
/// The import pipeline has three phases — initialize, prepare the queue, drain the queue — and
/// every one of them has to happen against the same <see cref="ImportContext"/>. The API
/// endpoints previously called only the first phase, so an upload created an orphaned archive
/// row and never produced a record. This runs all three inside a single request, which is valid
/// because the scoped context (and its DbContext) lives for the whole request.
/// </para>
/// </summary>
public class ImportRunner(
    ImportContextFactory importContextFactory,
    ArchiveService archiveService,
    StorageLocationService storageLocationService,
    ILogger<ImportRunner> logger)
{
    private const int BufferSize = 1024 * 1024;

    /// <summary>Default maximum size accepted by <see cref="RunStreamAsync"/>.</summary>
    public const long DefaultMaxPackageBytes = 100L * 1024 * 1024 * 1024;

    /// <summary>
    /// Imports every record found in the uploaded archive identified by <paramref name="objectKey"/>.
    /// </summary>
    /// <param name="objectKey">Object key returned by the chunked upload endpoints.</param>
    /// <param name="storageLocationId">
    /// Archive storage location to write blobs into. Falls back to the default archive location.
    /// </param>
    /// <param name="manifestType">
    /// Expected manifest type. When null the type is sniffed from the manifest.
    /// </param>
    public async Task<ImportRunResult> RunAsync(
        Guid objectKey,
        Guid? storageLocationId = null,
        ManifestType? manifestType = null,
        CancellationToken cancellationToken = default)
    {
        var archivePath = await archiveService.GetArchiveFileLocationAsync(objectKey.ToString());
        var result = await RunFileAsync(
            archivePath,
            storageLocationId,
            manifestType,
            cancellationToken);

        logger.LogInformation(
            "Imported {Count} record(s) as {ManifestType} {RecordId} from object key {ObjectKey}",
            result.ImportedCount, result.ManifestType, result.RecordId, objectKey);

        return result;
    }

    /// <summary>Imports every selected record from an LCX file already available to the server.</summary>
    public async Task<ImportRunResult> RunFileAsync(
        string archivePath,
        Guid? storageLocationId = null,
        ManifestType? manifestType = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        cancellationToken.ThrowIfCancellationRequested();

        var storageLocation = await storageLocationService
            .GetOrDefaultAsync(storageLocationId, StorageLocationType.Archive);

        if (storageLocation == null)
            throw new InvalidOperationException(
                "No archive storage location is configured to import into.");

        using var context = importContextFactory.Create();

        cancellationToken.ThrowIfCancellationRequested();
        var items = (await context.InitializeImportAsync(
            archivePath,
            manifestType,
            cancellationToken)).ToList();

        // Import everything the archive offers. The record selection UI exists for the Blazor
        // dialog; an API caller that uploaded a package wants all of it.
        var selectedRecordIds = items
            .Select(i => Guid.TryParse(i.Key, out var id) ? (Guid?)id : null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();

        cancellationToken.ThrowIfCancellationRequested();
        await context.PrepareImportQueueAsync(
            selectedRecordIds,
            storageLocation.Id,
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        await context.ImportQueueAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        return new ImportRunResult
        {
            RecordId = GetRecordId(context.Manifest),
            ManifestType = GetManifestType(context.Manifest),
            ImportedCount = context.Processed,
        };
    }

    /// <summary>
    /// Copies an LCX package stream to controlled temporary storage and runs the canonical import
    /// pipeline against it.
    /// </summary>
    /// <param name="package">Readable LCX package stream.</param>
    /// <param name="storageLocationId">
    /// Archive storage location to write blobs into. Falls back to the default archive location.
    /// </param>
    /// <param name="manifestType">
    /// Expected manifest type. When null the type is sniffed from the manifest.
    /// </param>
    /// <param name="maxPackageBytes">Maximum number of bytes accepted from the stream.</param>
    /// <param name="copyProgress">Receives the number of bytes copied to temporary storage.</param>
    public async Task<ImportRunResult> RunStreamAsync(
        Stream package,
        Guid? storageLocationId = null,
        ManifestType? manifestType = null,
        long maxPackageBytes = DefaultMaxPackageBytes,
        IProgress<long>? copyProgress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);

        if (!package.CanRead)
            throw new ArgumentException("The package stream must be readable.", nameof(package));

        if (maxPackageBytes <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(maxPackageBytes),
                "The maximum package size must be positive.");

        var packagePath = Path.Combine(
            Path.GetTempPath(),
            $"lancommander-import-{Guid.NewGuid():N}.lcx");
        long bytesTransferred = 0;

        try
        {
            await using (var output = new FileStream(
                packagePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[BufferSize];

                while (true)
                {
                    var read = await package.ReadAsync(buffer, cancellationToken);

                    if (read == 0)
                        break;

                    bytesTransferred += read;

                    if (bytesTransferred > maxPackageBytes)
                        throw new InvalidDataException(
                            $"The package exceeds the maximum size of {maxPackageBytes} bytes.");

                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    copyProgress?.Report(bytesTransferred);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            return await RunFileAsync(
                packagePath,
                storageLocationId,
                manifestType,
                cancellationToken);
        }
        finally
        {
            try
            {
                if (File.Exists(packagePath))
                    File.Delete(packagePath);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Could not delete temporary import package {PackagePath}",
                    packagePath);
            }
        }
    }

    // Root importers preserve the manifest Id for new records and replace it with the matched
    // persisted Id when an existing record is found by its natural key.
    private static Guid GetRecordId(object manifest) =>
        manifest is SDK.Models.Manifest.IKeyedModel keyed ? keyed.Id : Guid.Empty;

    private static ManifestType GetManifestType(object manifest) => manifest switch
    {
        SDK.Models.Manifest.Game => ManifestType.Game,
        SDK.Models.Manifest.Redistributable => ManifestType.Redistributable,
        SDK.Models.Manifest.Server => ManifestType.Server,
        SDK.Models.Manifest.Tool => ManifestType.Tool,
        _ => throw new InvalidOperationException(
            $"Unrecognized manifest type '{manifest?.GetType().Name ?? "null"}'."),
    };
}
