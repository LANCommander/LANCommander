using LANCommander.Server.Services.Providers.Metadata;

namespace LANCommander.Server.Services.Abstractions;

/// <summary>
/// Supplies metadata providers from outside this assembly, such as those plugins add, for
/// <see cref="MetadataService"/> to list alongside the built-in ones.
/// </summary>
public interface IMetadataProviderSource
{
    IEnumerable<IMetadataProvider> GetProviders();
}
