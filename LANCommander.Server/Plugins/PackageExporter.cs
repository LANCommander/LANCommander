using LANCommander.SDK.Enums;
using LANCommander.Server.ImportExport;
using LANCommander.Server.Services;

namespace LANCommander.Server.Plugins;

/// <summary>Gives plugins the server's <see cref="ExportContext"/> without exposing its dependencies.</summary>
/// <remarks>
/// An export context holds the record it was initialized with, so every call gets one of its own, in
/// a scope of its own, which disposes it.
/// </remarks>
internal sealed class PackageExporter(IServiceScopeFactory scopeFactory) : IPackageExporter
{
    public async Task<PackageContents> DescribeAsync(
        ImportExportRecordType recordType,
        Guid recordId,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var (export, items) = await InitializeAsync(scope.ServiceProvider, recordType, recordId, cancellationToken);

        return new PackageContents(
            recordId,
            recordType,
            export.GetName(),
            export.DataRecord.UpdatedOn,
            items
                .Select(item => new PackageItem(item.Id, item.Type, item.Name ?? "", item.Size, item.UpdatedOn))
                .ToList());
    }

    public async Task ExportAsync(
        ImportExportRecordType recordType,
        Guid recordId,
        Stream output,
        IEnumerable<Guid>? excludedItemIds = null,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var (export, items) = await InitializeAsync(scope.ServiceProvider, recordType, recordId, cancellationToken);

        var excluded = excludedItemIds?.ToHashSet() ?? [];

        await export.PrepareExportQueueAsync(items.Select(item => item.Id).Where(id => !excluded.Contains(id)).ToList());

        cancellationToken.ThrowIfCancellationRequested();

        await export.ExportQueueAsync(output);
    }

    /// <summary>The scope's export context, initialized with the record, and the child records it offers.</summary>
    static async Task<(ExportContext Export, IReadOnlyList<ImportExport.Models.ExportItemInfo> Items)> InitializeAsync(
        IServiceProvider services,
        ImportExportRecordType recordType,
        Guid recordId,
        CancellationToken cancellationToken)
    {
        var exists = recordType switch
        {
            ImportExportRecordType.Game => await services.GetRequiredService<GameService>().ExistsAsync(recordId),
            ImportExportRecordType.Redistributable => await services.GetRequiredService<RedistributableService>().ExistsAsync(recordId),
            ImportExportRecordType.Server => await services.GetRequiredService<ServerService>().ExistsAsync(recordId),
            ImportExportRecordType.Tool => await services.GetRequiredService<ToolService>().ExistsAsync(recordId),
            _ => throw new ArgumentException($"A {recordType} can't be exported on its own.", nameof(recordType)),
        };

        if (!exists)
            throw new KeyNotFoundException($"There is no {recordType} {recordId}.");

        cancellationToken.ThrowIfCancellationRequested();

        var export = services.GetRequiredService<ExportContext>();
        var items = (await export.InitializeExportAsync(recordId, recordType)).ToList();

        return (export, items);
    }
}
