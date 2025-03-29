using LANCommander.SDK.Enums;
using Microsoft.Extensions.DependencyInjection;
using SharpCompress.Archives.Zip;

namespace LANCommander.Server.Services.Importers;

public class ImportContextFactory<TRecord>(ServiceProvider serviceProvider) where TRecord : class
{
    public ImportContext<TRecord> Create<TRecord>(ZipArchive zipArchive, TRecord record, ImportRecordFlags flags)
    {
        return new ImportContext<TRecord>(serviceProvider, zipArchive, record);
    }
}