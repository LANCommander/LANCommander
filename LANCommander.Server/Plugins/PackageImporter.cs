using LANCommander.SDK.Enums;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.ImportExport.Services;

namespace LANCommander.Server.Plugins;

/// <summary>Gives plugins the server's <see cref="ImportRunner"/> without exposing its dependencies.</summary>
internal sealed class PackageImporter(ImportRunner runner) : IPackageImporter
{
    public async Task<PackageImportResult> ImportFileAsync(
        string packagePath,
        Guid? storageLocationId = null,
        ManifestType? manifestType = null,
        CancellationToken cancellationToken = default) =>
        ToResult(await runner.RunFileAsync(packagePath, storageLocationId, manifestType, cancellationToken));

    public async Task<PackageImportResult> ImportStreamAsync(
        Stream package,
        Guid? storageLocationId = null,
        ManifestType? manifestType = null,
        IProgress<long>? copyProgress = null,
        CancellationToken cancellationToken = default) =>
        ToResult(await runner.RunStreamAsync(
            package,
            storageLocationId,
            manifestType,
            copyProgress: copyProgress,
            cancellationToken: cancellationToken));

    static PackageImportResult ToResult(ImportRunResult result) =>
        new(result.RecordId, result.ManifestType, result.ImportedCount);
}
