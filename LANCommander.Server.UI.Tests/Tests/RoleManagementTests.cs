using LANCommander.Server.UI.Tests.Pages;
using Microsoft.Playwright;

namespace LANCommander.Server.UI.Tests.Tests;

/// <summary>
/// Tests for the Role Management UI at Settings > Roles.
/// Verifies adding, viewing, and deleting roles, and that the
/// Administrator role cannot be deleted.
/// </summary>
public class RoleManagementTests : IClassFixture<ConfiguredServerFixture>, IAsyncLifetime
{
    private readonly ConfiguredServerFixture _fixture;
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public RoleManagementTests(ConfiguredServerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        (_context, _page) = await _fixture.CreateLoggedInPageAsync();
    }

    public async Task DisposeAsync()
    {
        if (_page != null) await _page.CloseAsync();
        if (_context != null) await _context.DisposeAsync();
    }

    [Fact]
    public async Task RolesPage_ShowsAdministratorRole()
    {
        var rolesPage = new RolesPage(_page);
        await rolesPage.NavigateAsync();

        Assert.True(await rolesPage.IsRoleVisibleAsync("Administrator"),
            "Administrator role should be visible in the roles table");
    }

    [Fact]
    public async Task RolesPage_CanAddNewRole()
    {
        var rolesPage = new RolesPage(_page);
        await rolesPage.NavigateAsync();

        await rolesPage.AddRoleAsync("TestRole");

        Assert.True(await rolesPage.IsRoleVisibleAsync("TestRole"),
            "Newly added TestRole should appear in the roles table");
    }

    [Fact]
    public async Task RolesPage_CanDeleteRole()
    {
        var rolesPage = new RolesPage(_page);
        await rolesPage.NavigateAsync();

        // Add a role to delete
        await rolesPage.AddRoleAsync("RoleToDelete");
        Assert.True(await rolesPage.IsRoleVisibleAsync("RoleToDelete"),
            "RoleToDelete should be visible before deletion");

        // Delete the role
        await rolesPage.DeleteRoleAsync("RoleToDelete");

        Assert.False(await rolesPage.IsRoleVisibleAsync("RoleToDelete"),
            "RoleToDelete should no longer appear after deletion");
    }

    [Fact]
    public async Task RolesPage_AdministratorCannotBeDeleted()
    {
        var rolesPage = new RolesPage(_page);
        await rolesPage.NavigateAsync();

        Assert.True(await rolesPage.IsDeleteDisabledAsync("Administrator"),
            "The delete button for Administrator role should be disabled");
    }
}
