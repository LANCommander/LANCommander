using LANCommander.Server.Services.Importers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LANCommander.Server.Services;

public class ImportService(IServiceProvider serviceProvider) : IHostedService, IDisposable
{
    private ImportContext _importContext;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        using (var scope = serviceProvider.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<ImportContext>();
        }
        
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        // TODO release managed resources here
    }
}