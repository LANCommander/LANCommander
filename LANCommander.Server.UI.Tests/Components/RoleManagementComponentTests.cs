using Bunit;
using LANCommander.Server.Services;
using RolesIndex = LANCommander.Server.UI.Pages.Settings.Roles.Index;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>
/// bUnit component tests for the role management page. Replaces the Playwright
/// <c>SettingsTests.SettingsRoles_ShowsRoleList</c> assertion that the seeded
/// Administrator role appears in the data table. Exercises the custom
/// <c>DataTable</c> which loads its rows asynchronously after first render via
/// the EF <c>IDbContextFactory</c>.
/// </summary>
[Collection("BUnit")]
public class RoleManagementComponentTests : BUnitTestContext
{
    public RoleManagementComponentTests(BUnitServerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public void Roles_ShowsAddRoleButtonAndAdministratorRole()
    {
        var cut = RenderComponent<RolesIndex>();

        Assert.Contains(
            cut.FindAll("button"),
            b => b.TextContent.Contains("Add Role", StringComparison.OrdinalIgnoreCase));

        // The DataTable loads rows asynchronously after the first render, so poll until the
        // seeded Administrator role appears in the rendered markup.
        cut.WaitForAssertion(
            () => Assert.Contains(RoleService.AdministratorRoleName, cut.Markup),
            timeout: TimeSpan.FromSeconds(10));
    }
}
