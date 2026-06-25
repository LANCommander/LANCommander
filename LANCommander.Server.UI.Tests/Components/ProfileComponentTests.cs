using Bunit;
using ProfileIndex = LANCommander.Server.UI.Pages.Profile.Index;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>
/// bUnit component tests for the user profile page. Replaces the Playwright
/// <c>ProfileTests</c> assertions that only verified the page renders the current
/// user's details and form fields. Flows that depend on the logout redirect
/// (update alias, change password) remain in the Playwright smoke layer.
/// </summary>
[Collection("BUnit")]
public class ProfileComponentTests : BUnitTestContext
{
    public ProfileComponentTests(BUnitServerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public void Profile_ShowsCurrentUsername()
    {
        var cut = RenderComponent<ProfileIndex>();

        // The authenticated admin's username is bound into the username input.
        Assert.Contains(TestConstants.AdminUserName, cut.Markup);
    }

    [Fact]
    public void Profile_ShowsFormElements()
    {
        var cut = RenderComponent<ProfileIndex>();

        foreach (var label in new[] { "Username", "Alias", "Email Address" })
        {
            Assert.Contains(label, cut.Markup);
        }

        Assert.Contains(
            cut.FindAll("button"),
            b => b.TextContent.Contains("Save", StringComparison.OrdinalIgnoreCase));
    }
}
