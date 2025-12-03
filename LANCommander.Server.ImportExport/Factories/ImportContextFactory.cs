using LANCommander.Server.ImportExport.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.ImportExport.Factories;

public class ImportContextFactory(IServiceProvider serviceProvider)
{
    public ImportContext Create()
    {
        var importService = serviceProvider.GetRequiredService<ImportService>();
        
        var context = new ImportContext(serviceProvider);
        
        importService.AddContext(context);

        return context;
    }
}