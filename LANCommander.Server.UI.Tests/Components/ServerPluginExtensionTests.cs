using System.Security.Claims;
using System.Reflection;
using LANCommander.Server.Plugins;
using LANCommander.Server.UI;
using Microsoft.AspNetCore.Components;

namespace LANCommander.Server.UI.Tests.Components;

public class ServerPluginExtensionTests
{
    const string Administrator = "Administrator";

    [Fact]
    public void RouteResolverIncludesPluginAssembliesOnce()
    {
        var assembly = typeof(ServerPluginExtensionTests).Assembly;
        var pluginRoutes = new IServerRouteAssemblyExtension[]
        {
            new TestRouteAssemblyExtension(assembly),
            new TestRouteAssemblyExtension(assembly),
        };

        var routes = UIRouteAssembly.Resolve([], pluginRoutes);

        Assert.Equal([assembly], routes);
    }

    [Fact]
    public void PluginRouteResolverExcludesTheServerAssembly()
    {
        var pluginAssembly = typeof(ServerPluginExtensionTests).Assembly;
        var pluginRoutes = new IServerRouteAssemblyExtension[]
        {
            new TestRouteAssemblyExtension(typeof(Program).Assembly),
            new TestRouteAssemblyExtension(pluginAssembly),
        };

        var routes = UIRouteAssembly.ResolvePluginAssemblies(pluginRoutes);

        Assert.Equal([pluginAssembly], routes);
    }

    [Fact]
    public void NavigationDefaultsToAdministratorsOnly()
    {
        IServerNavigationExtension extension = new TestNavigationExtension();

        Assert.Equal("tests", extension.Id);
        Assert.Equal("Tests", extension.Label);
        Assert.Equal("/Plugins/Tests", extension.Href);
        Assert.Null(extension.Icon);
        Assert.Equal(0, extension.Order);
        Assert.Equal(PluginAccessPolicy.Administrator, extension.Access);
    }

    [Fact]
    public void NavigationIsHiddenFromAnonymousUsersByDefault()
    {
        IServerNavigationExtension extension = new TestNavigationExtension();

        Assert.False(PluginNavigationVisibility.IsVisible(extension, null));
        Assert.False(PluginNavigationVisibility.IsVisible(extension, Anonymous()));
    }

    [Fact]
    public void NavigationForAuthenticatedUsersIsHiddenFromAnonymousUsers()
    {
        IServerNavigationExtension extension =
            new TestNavigationExtension(PluginAccessPolicy.AuthenticatedUser);

        Assert.True(PluginNavigationVisibility.IsVisible(extension, User()));
        Assert.False(PluginNavigationVisibility.IsVisible(extension, Anonymous()));
        Assert.False(PluginNavigationVisibility.IsVisible(extension, null));
    }

    [Fact]
    public void NavigationWithRequiredRoleIsVisibleOnlyToUsersInThatRole()
    {
        IServerNavigationExtension extension =
            new TestNavigationExtension(PluginAccessPolicy.RequireRoles("Curator"));

        Assert.True(PluginNavigationVisibility.IsVisible(extension, User("Curator")));
        Assert.True(PluginNavigationVisibility.IsVisible(extension, User(Administrator)));
        Assert.False(PluginNavigationVisibility.IsVisible(extension, User()));
        Assert.False(PluginNavigationVisibility.IsVisible(extension, null));
    }

    [Fact]
    public void NavigationCannotAdvertiseAPageTheUserIsRefused()
    {
        // The entry claims every signed-in user may see it, but the target page is
        // administrator-only, so the link must stay hidden from non-administrators.
        IServerNavigationExtension extension = new TestNavigationExtension(
            PluginAccessPolicy.AuthenticatedUser,
            href: "/Plugins/AdminOnly");

        Assembly[] assemblies = [typeof(ServerPluginExtensionTests).Assembly];

        Assert.False(PluginNavigationVisibility.IsVisible(extension, User(), assemblies));
        Assert.True(PluginNavigationVisibility.IsVisible(extension, User(Administrator), assemblies));
    }

    [Fact]
    public void UndeclaredPluginPageIsAdministratorOnly()
    {
        Assert.Equal(PluginAccessPolicy.Administrator, PluginRouteAccess.ResolvePolicy(typeof(UndeclaredPage)));

        Assert.False(PluginRouteAccess.IsAuthorized(typeof(UndeclaredPage), null));
        Assert.False(PluginRouteAccess.IsAuthorized(typeof(UndeclaredPage), Anonymous()));
        Assert.False(PluginRouteAccess.IsAuthorized(typeof(UndeclaredPage), User()));
        Assert.True(PluginRouteAccess.IsAuthorized(typeof(UndeclaredPage), User(Administrator)));
    }

    [Fact]
    public void AuthenticatedUserPageRejectsAnonymousVisitors()
    {
        Assert.False(PluginRouteAccess.IsAuthorized(typeof(AuthenticatedPage), Anonymous()));
        Assert.True(PluginRouteAccess.IsAuthorized(typeof(AuthenticatedPage), User()));
        Assert.True(PluginRouteAccess.IsAuthorized(typeof(AuthenticatedPage), User(Administrator)));
    }

    [Fact]
    public void RolePageAcceptsNamedRolesAndAdministrators()
    {
        Assert.False(PluginRouteAccess.IsAuthorized(typeof(RolePage), User()));
        Assert.True(PluginRouteAccess.IsAuthorized(typeof(RolePage), User("Curator")));
        Assert.True(PluginRouteAccess.IsAuthorized(typeof(RolePage), User("moderator")));
        Assert.True(PluginRouteAccess.IsAuthorized(typeof(RolePage), User(Administrator)));
    }

    [Fact]
    public void RoleClaimsWithoutAuthenticationAreRejected()
    {
        // A principal carrying role claims but no authenticated identity must not pass.
        var unauthenticated = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.Role, Administrator)]));

        Assert.False(unauthenticated.Identity?.IsAuthenticated ?? false);
        Assert.False(PluginRouteAccess.IsAuthorized(typeof(RolePage), unauthenticated));
        Assert.False(PluginRouteAccess.IsAuthorized(typeof(AuthenticatedPage), unauthenticated));
    }

    [Fact]
    public void AttributeWithoutUsableRolesFallsBackToAdministrator()
    {
        Assert.Equal(PluginAccessPolicy.Administrator, PluginRouteAccess.ResolvePolicy(typeof(EmptyRolePage)));
        Assert.False(PluginRouteAccess.IsAuthorized(typeof(EmptyRolePage), User()));
        Assert.True(PluginRouteAccess.IsAuthorized(typeof(EmptyRolePage), User(Administrator)));
    }

    [Fact]
    public void RequireRolesRejectsAnEmptyRequirement() =>
        Assert.Throws<ArgumentException>(() => PluginAccessPolicy.RequireRoles("  ", ""));

    [Fact]
    public void PluginPageDetectionIsScopedToPluginAssemblies()
    {
        Assembly[] assemblies = [typeof(ServerPluginExtensionTests).Assembly];

        Assert.True(PluginRouteAccess.IsPluginPage(typeof(UndeclaredPage), assemblies));
        Assert.False(PluginRouteAccess.IsPluginPage(typeof(string), assemblies));
        Assert.False(PluginRouteAccess.IsPluginPage(typeof(UndeclaredPage), []));
        Assert.False(PluginRouteAccess.IsPluginPage(null, assemblies));
    }

    [Fact]
    public void RouteLookupMatchesDeclaredPluginRoutes()
    {
        Assembly[] assemblies = [typeof(ServerPluginExtensionTests).Assembly];

        Assert.Equal(typeof(AdminOnlyPage), PluginRouteAccess.FindPageType("/Plugins/AdminOnly", assemblies));
        Assert.Equal(typeof(AdminOnlyPage), PluginRouteAccess.FindPageType("/plugins/adminonly/", assemblies));
        Assert.Null(PluginRouteAccess.FindPageType("/Plugins/Missing", assemblies));
        Assert.Null(PluginRouteAccess.FindPageType(null, assemblies));
    }

    static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    static ClaimsPrincipal User(params string[] roles) =>
        new(new ClaimsIdentity(
            [.. roles.Select(role => new Claim(ClaimTypes.Role, role))],
            authenticationType: "Test"));

    private sealed record TestRouteAssemblyExtension(Assembly Assembly) : IServerRouteAssemblyExtension;

    private sealed class TestNavigationExtension(
        PluginAccessPolicy? access = null,
        string href = "/Plugins/Tests") : IServerNavigationExtension
    {
        public string Id => "tests";
        public string Label => "Tests";
        public string Href => href;
        public PluginAccessPolicy Access => access ?? PluginAccessPolicy.Administrator;
    }

    private sealed class UndeclaredPage : ComponentBase;

    [Route("/Plugins/AdminOnly")]
    private sealed class AdminOnlyPage : ComponentBase;

    [PluginAccess(PluginAccessLevel.AuthenticatedUser)]
    private sealed class AuthenticatedPage : ComponentBase;

    [PluginAccess("Curator", "Moderator")]
    private sealed class RolePage : ComponentBase;

    [PluginAccess("", "   ")]
    private sealed class EmptyRolePage : ComponentBase;
}
