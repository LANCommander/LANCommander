using LANCommander.Server.UI.Fixtures.Components;
using Microsoft.Playwright;

namespace LANCommander.Server.UI.Tests.Visual;

/// <summary>
/// Captures every control fixture from the gallery and compares it with its baseline, named
/// <c>Controls.{Fixture}</c>.
/// </summary>
[Collection("Visual")]
public class ControlFixtureVisualTests(VisualServerFixture fixture)
{
    public static TheoryData<string> FixtureNames()
    {
        var data = new TheoryData<string>();

        foreach (var componentFixture in ComponentFixtureCatalog.All)
            data.Add(componentFixture.Name);

        return data;
    }

    [Theory]
    [MemberData(nameof(FixtureNames))]
    public async Task Fixture_MatchesBaseline(string name)
    {
        var componentFixture = ComponentFixtureCatalog.Find(name)!;
        var page = await fixture.NewPageAsync();

        try
        {
            await page.GotoAsync($"/_fixtures/{name}");

            var frame = page.Locator($".fixture-frame[data-fixture='{name}'][data-ready]");
            await frame.WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });

            if (componentFixture.Hover != null)
                await page.HoverAsync(componentFixture.Hover);

            if (componentFixture.Click != null)
                await page.ClickAsync(componentFixture.Click);

            if (componentFixture.WaitFor != null)
                await page.Locator(componentFixture.WaitFor).First.WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });

            await VisualAssert.MatchesBaselineAsync(page, $"Controls.{name}", componentFixture.FullPage ? null : frame);
        }
        finally
        {
            await page.Context.DisposeAsync();
        }
    }
}
