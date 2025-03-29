using LANCommander.Server.Data.Models;
using Microsoft.Extensions.DependencyInjection;
using SharpCompress.Archives.Zip;

namespace LANCommander.Server.Services.Importers;

public class ImportContext<TRecord>(
    ServiceProvider serviceProvider,
    ZipArchive archive,
    TRecord record) : IDisposable
{
    public TRecord Record { get; } = record;
    public StorageLocation ArchiveStorageLocation { get; }
    public ZipArchive Archive { get; } = archive;
    
    public void Initialize()
    {
        
    }
    
    public void Dispose()
    {
        archive.Dispose();
    }
}