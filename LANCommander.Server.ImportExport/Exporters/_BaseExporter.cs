using LANCommander.Server.ImportExport.Models;

namespace LANCommander.Server.ImportExport.Exporters;

public abstract class BaseExporter<TRecord, TEntity> : IExporter<TRecord, TEntity>
{
    protected ExportContext ExportContext { get; private set; }

    public void UseContext(ExportContext context)
    {
        ExportContext = context;
    }

    /// <summary>
    /// Copies a file on disk into the export archive under the given entry name.
    /// </summary>
    /// <remarks>
    /// The source file is opened read-only and shared. Opening it for read/write (the FileStream
    /// default) fails whenever anything else holds a handle, such as the download endpoint
    /// streaming the same archive to a launcher, a backup agent, a virus scanner, or read-only
    /// storage, which silently drops the record from the export.
    ///
    /// The entry is only created once the source file is open, so a failure can't leave an empty
    /// entry stranded in the archive.
    /// </remarks>
    protected async Task WriteEntryFromFileAsync(string entryName, string path)
    {
        await using var sourceStream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        var entry = ExportContext.Archive.CreateEntry(entryName);

        await using var entryStream = entry.Open();

        await sourceStream.CopyToAsync(entryStream);
    }

    public abstract Task<ExportItemInfo> GetExportInfoAsync(TEntity record);
    public abstract bool CanExport(TRecord record);
    public abstract Task<TRecord> ExportAsync(Guid id);
}
