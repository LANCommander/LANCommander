using System.Collections.Concurrent;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Providers.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services;

public class MetadataService(IServiceProvider serviceProvider)
{
    // The providers are listed on every lookup; say once that a duplicate is ignored
    private static readonly ConcurrentDictionary<string, bool> WarnedDuplicates = new(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<string> GetProviderNames()
    {
        return GetProviders().Where(p => p.IsAvailable).Select(p => p.ProviderName);
    }

    public IMetadataProvider? GetProvider(string providerName)
    {
        return GetProviders().FirstOrDefault(p => p.ProviderName == providerName);
    }

    /// <summary>
    /// The built-in providers, then those from <see cref="IMetadataProviderSource"/>s. A provider
    /// named like one before it is skipped, so a plugin can't take over a built-in provider's name.
    /// </summary>
    private IEnumerable<IMetadataProvider> GetProviders()
    {
        var builtIn = serviceProvider.GetServices<IMetadataProvider>().ToList();
        var names = new HashSet<string>(builtIn.Select(p => p.ProviderName), StringComparer.OrdinalIgnoreCase);

        foreach (var provider in builtIn)
            yield return provider;

        foreach (var source in serviceProvider.GetServices<IMetadataProviderSource>())
        {
            foreach (var provider in source.GetProviders())
            {
                if (names.Add(provider.ProviderName))
                    yield return provider;
                else if (WarnedDuplicates.TryAdd(provider.ProviderName, true))
                    serviceProvider.GetService<ILogger<MetadataService>>()?.LogWarning(
                        "Ignoring metadata provider {Provider} from {Source}: another provider has that name",
                        provider.ProviderName, source.GetType().Name);
            }
        }
    }
}
