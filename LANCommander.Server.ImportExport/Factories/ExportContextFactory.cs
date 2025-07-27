using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.ImportExport.Factories;

public class ExportContextFactory(IServiceProvider serviceProvider)
{
    public ExportContext Create()
    {
        var scope = serviceProvider.CreateScope();

        return scope.ServiceProvider.GetService<ExportContext>();
    }
}