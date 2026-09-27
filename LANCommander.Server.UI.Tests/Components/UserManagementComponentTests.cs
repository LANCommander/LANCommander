using Bunit;
using LANCommander.Server.Services;
using Microsoft.Extensions.DependencyInjection;
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
        var cut = Render<UsersIndex>();

        // The DataTable loads rows asynchronously after first render; poll until the seeded
        // admin user appears.
        cut.WaitForAssertion(
            () => Assert.Contains(TestConstants.AdminUserName, cut.Markup),
            timeout: TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Users_DeletingTheOnlyAdministrator_IsRefused()
    {
        var cut = Render<UsersIndex>();

        cut.WaitForAssertion(
            () => Assert.Contains(TestConstants.AdminUserName, cut.Markup),
            timeout: TimeSpan.FromSeconds(10));

        var row = cut.FindAll("tr.rz-data-row").First(r => r.TextContent.Contains(TestConstants.AdminUserName));

        row.QuerySelector("button[title=Delete]")!.Click();
        cut.WaitForAssertion(() => cut.FindAll(".lc-popconfirm-actions button").First(b => b.TextContent.Contains("Delete")).Click());

        var notifications = Services.GetRequiredService<Radzen.NotificationService>();

        cut.WaitForAssertion(() => Assert.Contains(notifications.Messages, m => m.Summary == "Cannot delete the only administrator!"));

        var userService = Services.GetRequiredService<UserService>();

        Assert.NotNull(userService.GetAsync(TestConstants.AdminUserName).GetAwaiter().GetResult());
    }
}
