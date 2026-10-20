using LANCommander.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace LANCommander.Server.UI.Tests.Guards;

/// <summary>
/// Pages that administer the server, its content or other users must be limited to administrators,
/// and every other routed page must say who may see it. A bare [Authorize] once let any signed-in
/// user edit other users, and several server pages had no attribute at all.
/// </summary>
public class PageAuthorizationTests
{
    const string PagesNamespace = "LANCommander.Server.UI.Pages";

    static readonly string[] AdministratorAreas =
    [
        "Games", "Redistributables", "Tools", "Servers", "Settings", "Metadata", "Issues", "Scripting",
    ];

    // Public on purpose: rendered wiki pages, token redemption links, and setup before any account exists
    static readonly string[] PublicPages =
    [
        "Pages.Render", "Account.RedeemToken", "FirstTimeSetup.",
    ];

    static IEnumerable<Type> RoutedPages() =>
        typeof(LANCommander.Server.UI.Pages.Settings.General).Assembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith(PagesNamespace) == true)
            .Where(t => t.GetCustomAttributes(typeof(RouteAttribute), false).Any())
            .OrderBy(t => t.FullName);

    static string Area(Type page) => page.FullName![(PagesNamespace.Length + 1)..];

    static List<AuthorizeAttribute> Authorization(Type page) =>
        page.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().ToList();

    public static TheoryData<string> AdministratorPages()
    {
        var pages = new TheoryData<string>();

        foreach (var page in RoutedPages().Where(p => AdministratorAreas.Any(a => Area(p).StartsWith(a + "."))))
            pages.Add(page.FullName!);

        return pages;
    }

    public static TheoryData<string> OtherPages()
    {
        var pages = new TheoryData<string>();

        foreach (var page in RoutedPages().Where(p => !AdministratorAreas.Any(a => Area(p).StartsWith(a + "."))))
            pages.Add(page.FullName!);

        return pages;
    }

    static Type PageType(string fullName) => RoutedPages().Single(p => p.FullName == fullName);

    [Theory]
    [MemberData(nameof(AdministratorPages))]
    public void AdministratorPage_RequiresAdministrator(string page)
    {
        var roles = Authorization(PageType(page)).Select(a => a.Roles);

        Assert.Contains(roles, r => r?.Split(',').Contains(RoleService.AdministratorRoleName) == true);
    }

    [Theory]
    [MemberData(nameof(OtherPages))]
    public void OtherPage_RequiresSignIn_UnlessPublicOnPurpose(string page)
    {
        var type = PageType(page);

        if (PublicPages.Any(p => Area(type).StartsWith(p)))
            return;

        Assert.NotEmpty(Authorization(type));
    }
}
