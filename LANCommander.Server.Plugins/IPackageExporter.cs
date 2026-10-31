using LANCommander.SDK.Enums;

namespace LANCommander.Server.Plugins;

/// <summary>
/// Exports games, redistributables, servers and tools as LCX packages through the server's own export
/// pipeline, the same one the export dialog uses. Provided by the server; inject it into a scoped
/// plugin service or page.
/// </summary>
/// <remarks>
/// A package written here can be imported by another server's <see cref="IPackageImporter"/>.
/// </remarks>
public interface IPackageExporter
{
    /// <summary>
    /// Describes what a package of the record would hold: the record itself and every child record
    /// the export dialog would offer, such as archives, media, scripts and keys. Nothing is written.
    /// </summary>
    /// <param name="recordType">
    /// <see cref="ImportExportRecordType.Game"/>, <see cref="ImportExportRecordType.Redistributable"/>,
    /// <see cref="ImportExportRecordType.Server"/> or <see cref="ImportExportRecordType.Tool"/>.
    /// </param>
    /// <param name="recordId">Id of the record.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <exception cref="KeyNotFoundException">There is no such record.</exception>
    /// <exception cref="ArgumentException">The record type can't be exported on its own.</exception>
    Task<PackageContents> DescribeAsync(
        ImportExportRecordType recordType,
        Guid recordId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes an LCX package of the record to a stream, with every child record except those excluded.
    /// </summary>
    /// <param name="recordType">The kind of record, as for <see cref="DescribeAsync"/>.</param>
    /// <param name="recordId">Id of the record.</param>
    /// <param name="output">
    /// Where the package is written. It need not be seekable, but is written to synchronously, so an
    /// HTTP response body needs synchronous IO allowed.
    /// </param>
    /// <param name="excludedItemIds">
    /// Ids of child records, from <see cref="PackageContents.Items"/>, to leave out, for example
    /// archives the receiving server already holds.
    /// </param>
    /// <param name="cancellationToken">Cancels the export before it starts writing.</param>
    /// <exception cref="KeyNotFoundException">There is no such record.</exception>
    /// <exception cref="ArgumentException">The record type can't be exported on its own.</exception>
    Task ExportAsync(
        ImportExportRecordType recordType,
        Guid recordId,
        Stream output,
        IEnumerable<Guid>? excludedItemIds = null,
        CancellationToken cancellationToken = default);
}

/// <summary>What a package of a record would hold, from <see cref="IPackageExporter.DescribeAsync"/>.</summary>
/// <param name="RecordId">Id of the record.</param>
/// <param name="RecordType">The kind of record.</param>
/// <param name="Name">The record's title or name.</param>
/// <param name="UpdatedOn">When the record itself was last changed.</param>
/// <param name="Items">The child records a package would carry.</param>
public sealed record PackageContents(
    Guid RecordId,
    ImportExportRecordType RecordType,
    string Name,
    DateTime UpdatedOn,
    IReadOnlyList<PackageItem> Items);

/// <summary>A child record a package would carry.</summary>
/// <param name="Id">Id of the child record.</param>
/// <param name="Type">The kind of child record, e.g. <see cref="ImportExportRecordType.Archive"/>.</param>
/// <param name="Name">A name to show for it.</param>
/// <param name="Size">Bytes of file the package would carry for it, such as an archive's; 0 for none.</param>
/// <param name="UpdatedOn">When it was last changed, or null for records that don't track it.</param>
public sealed record PackageItem(
    Guid Id,
    ImportExportRecordType Type,
    string Name,
    long Size,
    DateTime? UpdatedOn);
