using Bunit;
using SettingsGeneral = LANCommander.Server.UI.Pages.Settings.General;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>
/// bUnit component tests for the admin Settings pages. Replaces the Playwright
/// <c>SettingsTests</c> assertions that verified each settings page renders its
/// expected form content. URL/routing assertions remain in the Playwright smoke layer.
/// </summary>
[Collection("BUnit")]
public class SettingsComponentTests : BUnitTestContext
{
    public SettingsComponentTests(BUnitServerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public void SettingsGeneral_ShowsFormElements()
    {
        var cut = RenderComponent<SettingsGeneral>();

        Assert.Contains("Port", cut.Markup);
        Assert.Contains("Use SSL", cut.Markup);
        Assert.Contains(
            cut.FindAll("button"),
            b => b.TextContent.Contains("Save", StringComparison.OrdinalIgnoreCase));
    }
}
