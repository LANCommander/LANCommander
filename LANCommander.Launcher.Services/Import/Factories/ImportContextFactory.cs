using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Launcher.Services.Import.Factories;

public class ImportContextFactory(IServiceProvider serviceProvider)
{
    public ImportContext Create()
    {
        var scope = serviceProvider.CreateScope();
        
        return new ImportContext(scope.ServiceProvider);
    }
}