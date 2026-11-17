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
    public void PluginMetadataSourcesJoinTheMetadataService() =>
        GetService<IEnumerable<Server.Services.Abstractions.IMetadataProviderSource>>()
            .ShouldContain(source => source is PluginMetadataProviders);

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

    [Fact]
    public async Task TheUserDirectoryChecksPasswords()
    {
        var user = await AddUserWithPasswordAsync("plugin-auth");
        var directory = GetService<IUserDirectory>();

        var result = await directory.CheckPasswordAsync(user.UserName!.ToUpperInvariant(), Password);

        result.Status.ShouldBe(PasswordCheckStatus.Succeeded);
        result.Account.ShouldNotBeNull();
        result.Account.Id.ShouldBe(user.Id);

        (await directory.CheckPasswordAsync(user.UserName!, "not-the-password")).Status.ShouldBe(PasswordCheckStatus.Failed);
        (await directory.CheckPasswordAsync(user.UserName!, "")).Status.ShouldBe(PasswordCheckStatus.Failed);

        var unknown = await directory.CheckPasswordAsync($"nobody-{Guid.NewGuid():N}", Password);

        unknown.Status.ShouldBe(PasswordCheckStatus.Failed);
        unknown.Account.ShouldBeNull();
    }

    [Fact]
    public async Task ALockedOutAccountIsRefusedEvenWithTheRightPassword()
    {
        var user = await AddUserWithPasswordAsync("plugin-locked", lockedOut: true);
        var directory = GetService<IUserDirectory>();

        var result = await directory.CheckPasswordAsync(user.UserName!, Password);

        result.Status.ShouldBe(PasswordCheckStatus.LockedOut);
        result.Succeeded.ShouldBeFalse();
        result.Account.ShouldBeNull();
    }

    [Fact]
    public async Task RepeatedPasswordFailuresAreRateLimited()
    {
        var user = await AddUserWithPasswordAsync("plugin-limited");
        var directory = GetService<IUserDirectory>();

        for (var i = 0; i < PasswordCheckRateLimiter.MaxFailures; i++)
            (await directory.CheckPasswordAsync(user.UserName!, "not-the-password")).Status.ShouldBe(PasswordCheckStatus.Failed);

        // Even the right password is refused until the failures expire
        var result = await directory.CheckPasswordAsync(user.UserName!, Password);

        result.Status.ShouldBe(PasswordCheckStatus.RateLimited);
        result.Account.ShouldBeNull();
        result.RetryAfter.ShouldNotBeNull();
        result.RetryAfter.Value.ShouldBeGreaterThan(TimeSpan.Zero);
        result.RetryAfter.Value.ShouldBeLessThanOrEqualTo(PasswordCheckRateLimiter.Window);
    }

    const string Password = "Plugin-Password-1234!";

    async Task<User> AddUserWithPasswordAsync(string prefix, bool lockedOut = false)
    {
        var userService = GetService<UserService>();

        var user = await userService.AddAsync(new User
        {
            UserName = $"{prefix}-{Guid.NewGuid():N}",
            Approved = true,
            LockoutEnabled = lockedOut,
            LockoutEnd = lockedOut ? DateTimeOffset.UtcNow.AddHours(1) : null,
        });

        (await userService.ChangePassword(user.UserName!, Password)).Succeeded.ShouldBeTrue();

        return user;
    }
}
