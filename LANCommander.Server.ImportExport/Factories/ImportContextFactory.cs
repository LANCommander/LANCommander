using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.ImportExport.Factories;

public class ImportContextFactory(IServiceProvider serviceProvider)
{
    public ImportContext Create()
    {
        var scope = serviceProvider.CreateScope();

        return scope.ServiceProvider.GetService<ImportContext>();
    }
}