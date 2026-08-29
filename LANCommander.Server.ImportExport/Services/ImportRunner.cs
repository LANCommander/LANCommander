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
        ManifestType? manifestType = null)
    {
        var archivePath = await archiveService.GetArchiveFileLocationAsync(objectKey.ToString());

        var storageLocation = await storageLocationService
            .GetOrDefaultAsync(storageLocationId, StorageLocationType.Archive);

        if (storageLocation == null)
            throw new InvalidOperationException(
                "No archive storage location is configured to import into.");

        using var context = importContextFactory.Create();

        var items = (await context.InitializeImportAsync(archivePath, manifestType)).ToList();

        // Import everything the archive offers. The record selection UI exists for the Blazor
        // dialog; an API caller that uploaded a package wants all of it.
        var selectedRecordIds = items
            .Select(i => Guid.TryParse(i.Key, out var id) ? (Guid?)id : null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();

        await context.PrepareImportQueueAsync(selectedRecordIds, storageLocation.Id);
        await context.ImportQueueAsync();

        var result = new ImportRunResult
        {
            RecordId = GetRecordId(context.Manifest),
            ManifestType = GetManifestType(context.Manifest),
            ImportedCount = context.Processed,
        };

        logger.LogInformation(
            "Imported {Count} record(s) as {ManifestType} {RecordId} from object key {ObjectKey}",
            result.ImportedCount, result.ManifestType, result.RecordId, objectKey);

        return result;
    }

    // Importers use the manifest's own Id as the entity primary key (see GameImporter.AddAsync),
    // so the root manifest Id is the id of the record that was created or updated.
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
