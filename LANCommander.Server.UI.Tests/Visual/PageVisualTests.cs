using LANCommander.Server.UI.Fixtures.Data;
using Microsoft.Playwright;

namespace LANCommander.Server.UI.Tests.Visual;

/// <summary>
/// Captures whole pages of the running server against the seeded fixture database, logged in as the
/// fixture administrator. Add a page here once its folder is migrated to the new controls.
/// </summary>
[Collection("Visual")]
public class PageVisualTests(VisualServerFixture fixture)
{
    /// <summary>Baseline name, route, and a selector that is visible once the page has loaded.</summary>
    public static TheoryData<string, string, string> Pages() => new()
    {
        { "Dashboard.Overview", "/", ".rz-chart svg path" },
        { "Dashboard.SessionPlaytime", "/Dashboard/SessionPlaytime", ".rz-data-row" },
        { "Issues.List", "/Issues", ".rz-data-row" },
        { "Issues.Open", $"/Issues/{FixtureIds.For("issue:neon-drift-controller")}", ".lc-form" },
        { "Issues.Resolved", $"/Issues/{FixtureIds.For("issue:arena-blitz-crash")}", ".lc-form" },
        { "Pages.Editor", $"/Pages/Edit/{FixtureIds.For("page:house-rules")}", ".lc-tree-node-selected" },
        { "Pages.View", "/Pages/Welcome/HouseRules", ".page-view-content" },
        { "Pages.NotFound", "/Pages/Missing", ".lc-result" },
        { "Metadata.Collections", "/Metadata/Collections", ".rz-data-row" },
        { "Metadata.Collection", $"/Metadata/Collections/{FixtureIds.For("collection:LAN Party Essentials")}", ".rz-data-row" },
        { "Metadata.Companies", "/Metadata/Companies", ".rz-data-row" },
        { "Metadata.Engines", "/Metadata/Engines", ".rz-data-row" },
        { "Metadata.Genres", "/Metadata/Genres", ".rz-data-row" },
        { "Metadata.Platforms", "/Metadata/Platforms", ".rz-data-row" },
        { "Metadata.Tags", "/Metadata/Tags", ".rz-data-row" },
        { "Profile.General", "/Profile", ".lc-form" },
        { "Profile.ChangePassword", "/Profile/ChangePassword", ".lc-form" },
        { "Profile.ConnectedAccounts", "/Profile/ConnectedAccounts", ".lc-empty" },
        { "Profile.Library", "/Profile/Library", ".rz-data-row" },
        { "Profile.Saves", "/Profile/Saves", ".rz-data-row" },
        { "Settings.General", "/Settings/General", ".lc-form" },
        { "Settings.Authentication", "/Settings/Authentication", ".lc-form" },
        { "Settings.Users", "/Settings/Users", ".rz-data-row" },
        { "Settings.UserEdit", "/Settings/Users/alex", ".lc-form" },
        { "Settings.UserChangePassword", $"/Settings/Users/{FixtureIds.For("user:alex")}/ChangePassword", ".lc-form" },
        { "Settings.Roles", "/Settings/Roles", ".rz-data-row" },
        { "Settings.RoleEdit", $"/Settings/Roles/{FixtureIds.For("role:players")}", ".lc-transfer" },
        { "Settings.Archives", "/Settings/Archives", ".rz-data-row" },
        { "Settings.UserSaves", "/Settings/UserSaves", ".lc-form" },
        { "Settings.Library", "/Settings/Library", ".lc-form" },
        { "Settings.Launcher", "/Settings/Launcher", ".lc-form" },
        { "Settings.Servers", "/Settings/Servers", ".lc-form" },
        { "Settings.Scripts", "/Settings/Scripts", ".lc-form" },
        { "Settings.Appearance", "/Settings/Appearance", ".lc-page-content" },
        { "Settings.Beacon", "/Settings/Beacon", ".lc-form" },
        { "Settings.IPXRelay", "/Settings/IPXRelay", ".lc-form" },
        { "Settings.IGDB", "/Settings/Integrations/IGDB", ".lc-form" },
        { "Settings.SteamGridDB", "/Settings/Integrations/SteamGridDB", ".lc-form" },
        { "Settings.PCGamingWiki", "/Settings/Integrations/PCGamingWiki", ".lc-form" },
        { "Settings.Tools", "/Settings/Tools", ".lc-page-content" },
        { "Settings.ActiveSessions", "/Settings/Tools/ActiveSessions", ".lc-table" },
        { "Settings.LongSessions", "/Settings/Tools/LongSessions", ".lc-table" },
        { "Settings.OptimizeImages", "/Settings/Tools/OptimizeImages", ".lc-table" },
        { "Games.List", "/Games", ".rz-data-row" },
        { "Games.Add", "/Games/Add", ".lc-form" },
        { "Games.General", $"/Games/{FixtureData.Games.Id(FixtureData.Games.ArenaBlitz)}/General", ".lc-page-content" },
        { "Games.Preview", $"/Games/{FixtureData.Games.Id(FixtureData.Games.ArenaBlitz)}/Preview", ".game-detail-preview" },
        { "Games.Actions", $"/Games/{FixtureData.Games.Id(FixtureData.Games.ArenaBlitz)}/Actions", ".lc-page-content" },
        { "Games.Archives", $"/Games/{FixtureData.Games.Id(FixtureData.Games.ArenaBlitz)}/Archives", ".lc-page-content" },
        { "Games.CustomFields", $"/Games/{FixtureData.Games.Id(FixtureData.Games.ArenaBlitz)}/CustomFields", ".lc-page-content" },
        { "Games.Expansions", $"/Games/{FixtureData.Games.Id(FixtureData.Games.ArenaBlitz)}/Expansions", ".lc-page-content" },
        { "Games.Keys", $"/Games/{FixtureData.Games.Id(FixtureData.Games.ArenaBlitz)}/Keys", ".lc-page-content" },
        { "Games.Media", $"/Games/{FixtureData.Games.Id(FixtureData.Games.ArenaBlitz)}/Media", ".lc-page-content" },
        { "Games.Mods", $"/Games/{FixtureData.Games.Id(FixtureData.Games.ArenaBlitz)}/Mods", ".lc-page-content" },
        { "Games.Multiplayer", $"/Games/{FixtureData.Games.Id(FixtureData.Games.ArenaBlitz)}/Multiplayer", ".lc-page-content" },
        { "Games.Options", $"/Games/{FixtureData.Games.Id(FixtureData.Games.ArenaBlitz)}/Options", ".lc-page-content" },
        { "Games.PlaySessions", $"/Games/{FixtureData.Games.Id(FixtureData.Games.ArenaBlitz)}/PlaySessions", ".lc-page-content" },
        { "Games.Redistributables", $"/Games/{FixtureData.Games.Id(FixtureData.Games.ArenaBlitz)}/Redistributables", ".lc-page-content" },
        { "Games.SavePaths", $"/Games/{FixtureData.Games.Id(FixtureData.Games.ArenaBlitz)}/SavePaths", ".lc-page-content" },
        { "Games.Saves", $"/Games/{FixtureData.Games.Id(FixtureData.Games.ArenaBlitz)}/Saves", ".lc-page-content" },
        { "Games.Scripts", $"/Games/{FixtureData.Games.Id(FixtureData.Games.ArenaBlitz)}/Scripts", ".lc-page-content" },
        { "Games.Versions", $"/Games/{FixtureData.Games.Id(FixtureData.Games.ArenaBlitz)}/Versions", ".lc-page-content" },
        { "Tools.List", "/Tools", ".rz-data-row" },
        { "Tools.General", $"/Tools/{FixtureData.Tools.Id(FixtureData.Tools.MapEditor)}/General", ".lc-page-content" },
        { "Tools.Archives", $"/Tools/{FixtureData.Tools.Id(FixtureData.Tools.MapEditor)}/Archives", ".lc-page-content" },
        { "Tools.Scripts", $"/Tools/{FixtureData.Tools.Id(FixtureData.Tools.MapEditor)}/Scripts", ".lc-page-content" },
        { "Tools.Actions", $"/Tools/{FixtureData.Tools.Id(FixtureData.Tools.MapEditor)}/Actions", ".lc-page-content" },
        { "Redistributables.List", "/Redistributables", ".rz-data-row" },
        { "Redistributables.General", $"/Redistributables/{FixtureData.Redistributables.Id(FixtureData.Redistributables.DirectX)}/General", ".lc-page-content" },
        { "Redistributables.Archives", $"/Redistributables/{FixtureData.Redistributables.Id(FixtureData.Redistributables.DirectX)}/Archives", ".lc-page-content" },
        { "Redistributables.Options", $"/Redistributables/{FixtureData.Redistributables.Id(FixtureData.Redistributables.DirectX)}/Options", ".lc-page-content" },
        { "Redistributables.Scripts", $"/Redistributables/{FixtureData.Redistributables.Id(FixtureData.Redistributables.DirectX)}/Scripts", ".lc-page-content" },
        { "Servers.List", "/Servers", ".rz-data-row" },
        { "Servers.General", $"/Servers/{FixtureData.Servers.Id(FixtureData.Servers.ArenaBlitzDedicated)}/General", ".lc-page-content" },
        { "Servers.Scripts", $"/Servers/{FixtureData.Servers.Id(FixtureData.Servers.ArenaBlitzDedicated)}/Scripts", ".lc-page-content" },
        { "Servers.Actions", $"/Servers/{FixtureData.Servers.Id(FixtureData.Servers.ArenaBlitzDedicated)}/Actions", ".lc-page-content" },
        { "Servers.Consoles", $"/Servers/{FixtureData.Servers.Id(FixtureData.Servers.ArenaBlitzDedicated)}/Consoles", ".lc-page-content" },
        { "Servers.HTTP", $"/Servers/{FixtureData.Servers.Id(FixtureData.Servers.ArenaBlitzDedicated)}/HTTP", ".lc-page-content" },
        { "Servers.Monitor", $"/Servers/{FixtureData.Servers.Id(FixtureData.Servers.ArenaBlitzDedicated)}/Monitor", ".server-monitor-rail" },
        { "Servers.Autostart", $"/Servers/{FixtureData.Servers.Id(FixtureData.Servers.ArenaBlitzDedicated)}/Autostart", ".lc-page-content" },
        { "Chat", "/Chat", ".chat" },
    };

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task Page_MatchesBaseline(string name, string route, string readySelector)
    {
        var page = await fixture.NewAdminPageAsync();

        try
        {
            await page.GotoAsync(route);
            await page.Locator(readySelector).First.WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });

            // Tables and cards load their data after the first render; wait for spinners to clear
            await WaitForSpinnersAsync(page);

            await VisualAssert.MatchesBaselineAsync(page, $"Pages.{name}");
        }
        finally
        {
            await page.Context.DisposeAsync();
        }
    }

    /// <summary>
    /// The Games list narrowed the way a large library is triaged: a view and a facet from the rail,
    /// the chips that explain the count, and a selection carried across every matching game.
    /// </summary>
    [Fact]
    public async Task GamesTriage_MatchesBaseline()
    {
        var page = await fixture.NewAdminPageAsync();

        try
        {
            await page.GotoAsync("/Games");
            await page.Locator(".rz-data-row").First.WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });

            await page.Locator(".games-rail-view", new PageLocatorOptions { HasText = "Missing art" }).ClickAsync();

            // Genre starts collapsed; open it, then pick its first entry
            var genre = page.Locator(".games-rail-group", new PageLocatorOptions { HasText = "Genre" });
            await genre.Locator(".games-rail-group-toggle").ClickAsync();
            await genre.Locator(".games-rail-facet").First.ClickAsync();
            await page.Locator(".games-chips").WaitForAsync();

            await page.Locator("tbody .lc-table-select .rz-chkbox-box").First.ClickAsync();
            await page.Locator(".games-bulkbar button", new PageLocatorOptions { HasText = "matching" }).ClickAsync();
            await page.Locator(".games-bulkbar button", new PageLocatorOptions { HasText = "matching" }).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });

            await WaitForSpinnersAsync(page);

            await VisualAssert.MatchesBaselineAsync(page, "Pages.Games.Triage");
        }
        finally
        {
            await page.Context.DisposeAsync();
        }
    }

    /// <summary>The Games list as cover tiles, where games without art show the launcher's placeholder.</summary>
    [Fact]
    public async Task GamesGrid_MatchesBaseline()
    {
        var page = await fixture.NewAdminPageAsync();

        try
        {
            await page.GotoAsync("/Games");
            await page.Locator(".rz-data-row").First.WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });

            await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Grid", Exact = true }).ClickAsync();
            await page.Locator(".games-tile").First.WaitForAsync();

            await VisualAssert.MatchesBaselineAsync(page, "Pages.Games.Grid");
        }
        finally
        {
            await page.Context.DisposeAsync();
        }
    }

    /// <summary>The script editor: scripts, the editor over its console, the script's fields, and the status bar.</summary>
    [Fact]
    public async Task ScriptEditor_MatchesBaseline()
    {
        var page = await fixture.NewAdminPageAsync();

        try
        {
            await page.GotoAsync($"/Games/{FixtureData.Games.Id(FixtureData.Games.ArenaBlitz)}/Scripts");
            await page.Locator(".rz-data-row").First.WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });

            await page.Locator(".rz-data-row button[title='Edit']").First.ClickAsync();
            await page.Locator(".script-editor .monaco-editor").WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });

            await VisualAssert.MatchesBaselineAsync(page, "Pages.Games.ScriptEditor");
        }
        finally
        {
            await page.Context.DisposeAsync();
        }
    }

    /// <summary>
    /// Waits until no spinner is left. A page can show many at once (an image per row of a large
    /// list), so this waits for none rather than for the one.
    /// </summary>
    static Task WaitForSpinnersAsync(IPage page) =>
        Assertions.Expect(page.Locator(".lc-spin, .rz-datatable-loading"))
            .ToHaveCountAsync(0, new LocatorAssertionsToHaveCountOptions { Timeout = 15000 });

    /// <summary>Pages seen before logging in.</summary>
    public static TheoryData<string, string, string> AnonymousPages() => new()
    {
        { "Account.Login", "/Login", "form#account" },
    };

    [Theory]
    [MemberData(nameof(AnonymousPages))]
    public async Task AnonymousPage_MatchesBaseline(string name, string route, string readySelector)
    {
        var page = await fixture.NewPageAsync();

        try
        {
            await page.GotoAsync(route);
            await page.Locator(readySelector).First.WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });

            // The launcher download button loads its list after the page is interactive
            await page.Locator(".login-download-pane .lc-button").WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });

            await VisualAssert.MatchesBaselineAsync(page, $"Pages.{name}");
        }
        finally
        {
            await page.Context.DisposeAsync();
        }
    }
}
