using LANCommander.Server.UI.Tests.Pages;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace LANCommander.Server.UI.Tests.Tests;

/// <summary>
/// Tests for the Role Management UI at Settings > Roles.
/// Verifies adding, viewing, and deleting roles, and that the
/// Administrator role cannot be deleted.
/// </summary>
[Collection("Server")]
public class RoleManagementTests : IAsyncLifetime
{
    private readonly ConfiguredServerFixture _fixture;
    private readonly ITestOutputHelper _output;
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public RoleManagementTests(ConfiguredServerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    public async Task InitializeAsync()
    {
        (_context, _page) = await _fixture.CreateLoggedInPageAsync();
    }

    public async Task DisposeAsync()
    {
        await ScreenshotHelper.CaptureAsync(_page, _output);
        if (_page != null) await _page.CloseAsync();
        if (_context != null) await _context.DisposeAsync();
    }

    [Fact]
    public async Task RolesPage_ShowsAdministratorRole()
    {
        var rolesPage = new RolesPage(_page);
        await rolesPage.NavigateAsync();

        await Assertions.Expect(rolesPage.Role("Administrator")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task RolesPage_CanAddNewRole()
    {
        var rolesPage = new RolesPage(_page);
        await rolesPage.NavigateAsync();

        await rolesPage.AddRoleAsync("TestRole");

        await Assertions.Expect(rolesPage.Role("TestRole")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task RolesPage_CanDeleteRole()
    {
        var rolesPage = new RolesPage(_page);
        await rolesPage.NavigateAsync();

        // Add a role to delete
        await rolesPage.AddRoleAsync("RoleToDelete");
        await Assertions.Expect(rolesPage.Role("RoleToDelete")).ToBeVisibleAsync();

        // Delete the role
        await rolesPage.DeleteRoleAsync("RoleToDelete");

        await Assertions.Expect(rolesPage.Role("RoleToDelete")).ToBeHiddenAsync();
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
