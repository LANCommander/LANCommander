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

    [Fact]
    public async Task TheUserDirectoryFindsAccountsWithTheirRolesAndSignInState()
    {
        var admin = await EnsureAdminUserCreatedAsync();
        var userService = GetService<UserService>();
        var directory = GetService<IUserDirectory>();

        var lockedOut = await userService.AddAsync(new User
        {
            UserName = $"locked-{Guid.NewGuid():N}",
            Alias = "Locked Out",
            Approved = true,
            LockoutEnabled = true,
            LockoutEnd = DateTimeOffset.UtcNow.AddHours(1),
        });

        var found = await directory.FindAsync(admin.UserName!.ToUpperInvariant());

        found.ShouldNotBeNull();
        found.Id.ShouldBe(admin.Id);
        found.IsAdministrator.ShouldBeTrue();
        found.Roles.ShouldContain(RoleService.AdministratorRoleName);
        found.CanSignIn.ShouldBeTrue();

        var locked = await directory.GetAsync(lockedOut.Id);

        locked.ShouldNotBeNull();
        locked.Alias.ShouldBe("Locked Out");
        locked.IsAdministrator.ShouldBeFalse();
        locked.CanSignIn.ShouldBeFalse();

        (await directory.GetAsync()).ShouldContain(account => account.Id == lockedOut.Id);
        (await directory.FindAsync("nobody-by-this-name")).ShouldBeNull();
    }
}
