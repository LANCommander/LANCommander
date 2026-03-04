using LANCommander.Server.Services.Providers.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services;

public class MetadataService(IServiceProvider serviceProvider)
{
    public IEnumerable<string> GetProviderNames()
    {
        var providers = serviceProvider.GetServices<IMetadataProvider>();

        return providers.Select(p => p.ProviderName);
    }

    public IMetadataProvider? GetProvider(string providerName)
    {
        var providers = serviceProvider.GetServices<IMetadataProvider>();
        
        return providers.FirstOrDefault(p => p.ProviderName == providerName);
    }
}