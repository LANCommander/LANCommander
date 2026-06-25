using Bunit;
using UsersIndex = LANCommander.Server.UI.Pages.Settings.Users.Index;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>
/// bUnit component tests for the user management page. Replaces the Playwright
/// <c>SettingsTests.SettingsUsers_ShowsUserList</c> assertion that the seeded
/// admin user appears in the data table.
/// </summary>
[Collection("BUnit")]
public class UserManagementComponentTests : BUnitTestContext
{
    public UserManagementComponentTests(BUnitServerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public void Users_ShowsSeededAdminUser()
    {
        var cut = RenderComponent<UsersIndex>();

        // The DataTable loads rows asynchronously after first render; poll until the seeded
        // admin user appears.
        cut.WaitForAssertion(
            () => Assert.Contains(TestConstants.AdminUserName, cut.Markup),
            timeout: TimeSpan.FromSeconds(10));
    }
}
