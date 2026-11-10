using LANCommander.Server.ImportExport.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.ImportExport.Factories;

public class ImportContextFactory(IServiceProvider serviceProvider)
{
    /// <summary>
    /// Creates a context with its own DI scope. The importers are scoped and hold per-import state
    /// (the context they serve, files waiting to be written), so contexts sharing a scope, such as
    /// two in one Blazor session, would import with each other's state. Dispose the context to
    /// release the scope.
    /// </summary>
    public ImportContext Create()
    {
        var importService = serviceProvider.GetRequiredService<ImportService>();

        var context = new ImportContext(serviceProvider.CreateScope());

        importService.AddContext(context);

        return context;
    }
}
