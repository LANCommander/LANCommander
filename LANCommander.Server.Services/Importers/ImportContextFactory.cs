using LANCommander.SDK.Enums;
using Microsoft.Extensions.DependencyInjection;
using SharpCompress.Archives.Zip;

namespace LANCommander.Server.Services.Importers;

public class ImportContextFactory(ServiceProvider serviceProvider)
{
    public ImportContext<TRecord> Create<TRecord>(ZipArchive zipArchive, ImportRecordFlags flags)
    {
        var context = new ImportContext<TRecord>(serviceProvider, zipArchive, record);

        context.Initialize(zipArchive, record);

        return context;
    }
}