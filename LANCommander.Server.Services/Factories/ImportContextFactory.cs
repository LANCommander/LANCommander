using LANCommander.Server.Services.Importers;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Factories;

public class ImportContextFactory(IServiceProvider serviceProvider)
{
    public ImportContext Create()
    {
        var scope = serviceProvider.CreateScope();

        return scope.ServiceProvider.GetService<ImportContext>();
    }
}