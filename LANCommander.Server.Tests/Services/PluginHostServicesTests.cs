using LANCommander.SDK.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Plugins;
using LANCommander.Server.Services;
using Shouldly;

namespace LANCommander.Server.Tests.Services;

/// <summary>
/// The services the server offers plugins through LANCommander.Server.Plugins, resolved from the
/// real application container the way a plugin resolves them.
/// </summary>
[Collection("Application")]
public class PluginHostServicesTests(ApplicationFixture fixture) : BaseTest(fixture)
{
    [Fact]
    public async Task StorageLocationsAreListedByTypeWithTheDefaultFirst()
    {
        var storageLocationService = GetService<StorageLocationService>();
        var provider = GetService<IStorageLocationProvider>();

        var added = await storageLocationService.AddAsync(new StorageLocation
        {
            Path = GetTemporaryDirectory(),
            Type = StorageLocationType.Archive,
            Default = false,
        });

        var locations = await provider.GetAsync(StorageLocationType.Archive);

        locations.ShouldAllBe(location => location.Type == StorageLocationType.Archive);
        locations.ShouldContain(location => location.Id == added.Id && location.Path == added.Path && !location.Default);

        if (locations.Any(location => location.Default))
            locations[0].Default.ShouldBeTrue();
    }

    [Fact]
    public void ThePackageImporterIsAvailableToPlugins() =>
        GetService<IPackageImporter>().ShouldNotBeNull();

    [Fact]
    public void MetadataLookupIsAvailableToPlugins() =>
        GetService<IMetadataLookup>().ShouldNotBeNull();
}
