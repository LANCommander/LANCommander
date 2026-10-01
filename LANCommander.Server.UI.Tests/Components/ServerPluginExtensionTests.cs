using System.Security.Claims;
using System.Reflection;
using LANCommander.Server.Plugins;
using LANCommander.Server.UI;

namespace LANCommander.Server.UI.Tests.Components;

public class ServerPluginExtensionTests
{
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
    public void NavigationDefaultsAreHostIndependent()
    {
        IServerNavigationExtension extension = new TestNavigationExtension();

        Assert.Equal("tests", extension.Id);
        Assert.Equal("Tests", extension.Label);
        Assert.Equal("/Plugins/Tests", extension.Href);
        Assert.Null(extension.Icon);
        Assert.Equal(0, extension.Order);
        Assert.Null(extension.RequiredRole);
    }

    [Fact]
    public void NavigationWithoutRequiredRoleIsVisibleToAnonymousUsers()
    {
        IServerNavigationExtension extension = new TestNavigationExtension();

        Assert.True(PluginNavigationVisibility.IsVisible(extension, null));
    }

    [Fact]
    public void NavigationWithRequiredRoleIsVisibleOnlyToUsersInThatRole()
    {
        IServerNavigationExtension extension = new TestNavigationExtension("Administrator");
        var authorized = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, "Administrator")],
            authenticationType: "Test"));
        var unauthorized = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "Test"));

        Assert.True(PluginNavigationVisibility.IsVisible(extension, authorized));
        Assert.False(PluginNavigationVisibility.IsVisible(extension, unauthorized));
        Assert.False(PluginNavigationVisibility.IsVisible(extension, null));
    }

    private sealed record TestRouteAssemblyExtension(Assembly Assembly) : IServerRouteAssemblyExtension;

    private sealed class TestNavigationExtension(string? requiredRole = null) : IServerNavigationExtension
    {
        public string Id => "tests";
        public string Label => "Tests";
        public string Href => "/Plugins/Tests";
        public string? RequiredRole => requiredRole;
    }
}
