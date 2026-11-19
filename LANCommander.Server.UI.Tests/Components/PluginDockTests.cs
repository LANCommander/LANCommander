using System.Security.Claims;
using Bunit;
using LANCommander.Server.Plugins;
using LANCommander.Server.Services;
using LANCommander.Server.UI.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>
/// Plugin bars docked to the top or bottom of the shell through <see cref="IServerDockExtension"/>:
/// which are shown, to whom, in what order, and that one failing leaves the rest.
/// </summary>
[Collection("BUnit")]
public class PluginDockTests : BUnitTestContext
{
    public PluginDockTests(BUnitServerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public void ShowsTheBarsDockedToItsEdge_TheFirstAgainstTheEdge()
    {
        Add(new Dock("player", ServerDockPosition.Bottom, typeof(Bar)));
        Add(new Dock("status", ServerDockPosition.Bottom, typeof(Bar), order: -1));
        Add(new Dock("banner", ServerDockPosition.Top, typeof(Bar)));

        var bottom = Render<PluginDock>(parameters => parameters.Add(p => p.Position, ServerDockPosition.Bottom));
        var top = Render<PluginDock>(parameters => parameters.Add(p => p.Position, ServerDockPosition.Top));

        // "status" orders first, so it is the lowest bar: last in the bottom dock's markup
        Assert.Equal(["player", "status"], Docked(bottom));
        Assert.NotNull(bottom.Find(".lc-dock.lc-dock-bottom"));
        Assert.Equal(["banner"], Docked(top));
        Assert.NotNull(top.Find(".lc-dock.lc-dock-top"));
    }

    [Fact]
    public void RendersNothingWithoutBars()
    {
        var cut = Render<PluginDock>(parameters => parameters.Add(p => p.Position, ServerDockPosition.Bottom));

        Assert.Empty(cut.FindAll(".lc-dock"));
    }

    [Fact]
    public void ShowsBarsOnlyToThoseTheirPolicyAdmits()
    {
        Authorization.SetRoles();

        Add(new Dock("admin", ServerDockPosition.Bottom, typeof(Bar)));
        Add(new Dock("everyone", ServerDockPosition.Bottom, typeof(Bar), access: PluginAccessPolicy.AuthenticatedUser));

        var cut = Render<PluginDock>(parameters => parameters.Add(p => p.Position, ServerDockPosition.Bottom));

        Assert.Equal(["everyone"], Docked(cut));
    }

    [Fact]
    public void ABarThatFailsIsReplacedByAnError_TheOthersStay()
    {
        Add(new Dock("broken", ServerDockPosition.Bottom, typeof(Broken)));
        Add(new Dock("player", ServerDockPosition.Bottom, typeof(Bar)));

        var cut = Render<PluginDock>(parameters => parameters.Add(p => p.Position, ServerDockPosition.Bottom));

        Assert.Contains("broken could not be displayed", cut.Find("[data-dock='broken']").TextContent);
        Assert.Equal("player", cut.Find("[data-dock='player'] .test-bar").TextContent);
    }

    [Fact]
    public void LeavesOutBarsThatCantBeShown()
    {
        IServerDockExtension[] extensions =
        [
            new Dock("player", ServerDockPosition.Bottom, typeof(Bar)),
            new Dock("has spaces", ServerDockPosition.Bottom, typeof(Bar)),
            new Dock("", ServerDockPosition.Bottom, typeof(Bar)),
            new Dock("not-a-component", ServerDockPosition.Bottom, typeof(string)),
            new Dock("nowhere", (ServerDockPosition)42, typeof(Bar)),
            new Dock("twin", ServerDockPosition.Bottom, typeof(Bar)),
            new Dock("TWIN", ServerDockPosition.Top, typeof(Bar)),
            new Dock("throws", ServerDockPosition.Bottom, typeof(Bar), throws: true),
        ];

        var docked = PluginDocks.Resolve(extensions, ServerDockPosition.Bottom, Admin(), NullLogger.Instance);

        Assert.Equal(["player"], docked.Select(d => d.Id));
    }

    [Fact]
    public void OrdersByOrderThenId()
    {
        IServerDockExtension[] extensions =
        [
            new Dock("c", ServerDockPosition.Top, typeof(Bar)),
            new Dock("b", ServerDockPosition.Top, typeof(Bar), order: 1),
            new Dock("a", ServerDockPosition.Top, typeof(Bar)),
        ];

        var docked = PluginDocks.Resolve(extensions, ServerDockPosition.Top, Admin(), NullLogger.Instance);

        Assert.Equal(["a", "c", "b"], docked.Select(d => d.Id));
    }

    void Add(IServerDockExtension dock) => Services.AddSingleton(dock);

    static List<string> Docked(IRenderedComponent<PluginDock> cut) =>
        cut.FindAll(".lc-dock-item").Select(item => item.GetAttribute("data-dock")!).ToList();

    static ClaimsPrincipal Admin() =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Role, RoleService.AdministratorRoleName)], authenticationType: "Test"));

    private sealed class Dock(
        string id,
        ServerDockPosition position,
        Type component,
        int order = 0,
        PluginAccessPolicy? access = null,
        bool throws = false) : IServerDockExtension
    {
        public string Id => id;
        public ServerDockPosition Position => position;
        public Type Component => throws ? throw new InvalidOperationException("No component") : component;
        public int Order => order;
        public PluginAccessPolicy Access => access ?? PluginAccessPolicy.Administrator;
    }

    public sealed class Bar : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "class", "test-bar");
            builder.AddContent(2, "player");
            builder.CloseElement();
        }
    }

    public sealed class Broken : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder) =>
            throw new InvalidOperationException("The bar broke");
    }
}
