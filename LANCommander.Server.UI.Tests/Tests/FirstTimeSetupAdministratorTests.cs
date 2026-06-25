using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using LANCommander.Server.Settings.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.UI.Tests.Tests;

/// <summary>
/// Fixture for the first-time-setup administrator flow. Stands up the real server DI container
/// backed by a file-based SQLite database (via <see cref="UITestApplicationFactory"/>) but, unlike
/// <c>BUnitServerFixture</c>, seeds NO administrator role or user. The provider is marked as
/// configured so <see cref="SetupService.IsSetupInitialized"/> queries the database and reports the
/// server as "installed but awaiting an admin" — exactly the state the Administrator wizard step
/// runs in.
/// </summary>
public class FirstTimeSetupFixture : IAsyncLifetime
{
    public UITestApplicationFactory Factory { get; private set; } = null!;

    public Task InitializeAsync()
    {
        Factory = new UITestApplicationFactory();
        // Trigger the factory to build the host and create the SQLite schema.
        _ = Factory.Services;

        // Mark the provider as configured (but seed no admin) so the server behaves like a freshly
        // installed instance sitting on the final "create administrator" step.
        DatabaseContext.Provider = DatabaseProvider.SQLite;

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        DatabaseContext.Provider = DatabaseProvider.Unknown;
        await Factory.DisposeAsync();
    }
}

/// <summary>
/// Isolates the first-time-setup administrator tests in their own collection so the seeded admin
/// they create does not contend with the configured/bUnit collections over the static
/// <see cref="DatabaseContext.Provider"/> and shared database.
/// </summary>
[CollectionDefinition("FirstTimeSetupAdministrator")]
public class FirstTimeSetupAdministratorCollection : ICollectionFixture<FirstTimeSetupFixture>
{
}

/// <summary>
/// Integration tests for the final first-time-setup step (<c>Administrator.razor</c>): creating the
/// initial administrator account. These exercise the exact service calls the page makes against a
/// real SQLite-backed DI container, guarding the install flow that a user can only ever run once.
///
/// Regression: the admin is created through <see cref="UserService.AddAsync"/>'s password-policy
/// bypass path, which writes the user directly via <c>DbContext</c>. That path previously left
/// <c>NormalizedUserName</c> null, so ASP.NET Identity's <c>FindByNameAsync</c> (used by
/// <see cref="UserService.AddToRolesAsync"/>) could not locate the just-created user and the wizard
/// failed with "Value cannot be null. (Parameter 'user')".
/// </summary>
[Collection("FirstTimeSetupAdministrator")]
public class FirstTimeSetupAdministratorTests
{
    private readonly FirstTimeSetupFixture _fixture;

    public FirstTimeSetupAdministratorTests(FirstTimeSetupFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateAdministrator_FollowingWizardFlow_ProducesUsableAdminAndCompletesSetup()
    {
        const string username = "setupadmin";
        const string password = "SetupAdmin123!";

        using var scope = _fixture.Factory.RealServices.CreateScope();
        var setupService = scope.ServiceProvider.GetRequiredService<SetupService>();
        var roleService = scope.ServiceProvider.GetRequiredService<RoleService>();
        var userService = scope.ServiceProvider.GetRequiredService<UserService>();

        // A freshly installed server with no administrator yet is not considered set up.
        Assert.False(await setupService.IsSetupInitialized());

        // Mirror Administrator.razor: ensure the Administrator role exists, then create the admin
        // user through the password-policy bypass path and assign the role.
        var role = await roleService.GetAsync(RoleService.AdministratorRoleName)
                   ?? await roleService.AddAsync(new Role { Name = RoleService.AdministratorRoleName });
        Assert.NotNull(role);

        var user = new User
        {
            UserName = username,
            Approved = true,
            ApprovedOn = DateTime.UtcNow,
        };

        await userService.AddAsync(user, bypassPasswordPolicy: true, password);

        // The regression point: before NormalizedUserName was populated in the bypass path, the
        // FindByNameAsync inside AddToRolesAsync returned null and this threw
        // ArgumentNullException ("Value cannot be null. (Parameter 'user')").
        await userService.AddToRolesAsync(user.UserName, new[] { RoleService.AdministratorRoleName });

        // The new admin is findable by name and carries the administrator role.
        var persisted = await userService.GetAsync(username);
        Assert.NotNull(persisted);
        Assert.True(await userService.IsInRoleAsync(persisted, RoleService.AdministratorRoleName));

        var administrators = await roleService.GetUsersAsync(RoleService.AdministratorRoleName);
        Assert.Contains(administrators, u => u.UserName == username);

        // The password set through the bypass path actually authenticates.
        Assert.True(await userService.CheckPassword(username, password));

        // With an administrator present, setup now reports as complete.
        Assert.True(await setupService.IsSetupInitialized());
    }

    [Fact]
    public async Task AddAsync_WithPasswordPolicyBypass_PopulatesNormalizedUserName()
    {
        const string username = "normalizationcheck";

        using var scope = _fixture.Factory.RealServices.CreateScope();
        var userService = scope.ServiceProvider.GetRequiredService<UserService>();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();

        var user = new User { UserName = username };

        await userService.AddAsync(user, bypassPasswordPolicy: true, "Whatever123!");

        // The directly-inserted row must carry a normalized user name, otherwise Identity's
        // FindByNameAsync (which queries NormalizedUserName) cannot locate the user.
        await using var db = await contextFactory.CreateDbContextAsync();
        var stored = await db.Users.AsNoTracking().SingleAsync(u => u.UserName == username);
        Assert.Equal(username.ToUpperInvariant(), stored.NormalizedUserName);
    }
}
