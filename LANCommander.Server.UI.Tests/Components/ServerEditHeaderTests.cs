using Bunit;
using LANCommander.SDK.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using LANCommander.Server.Settings.Enums;
using LANCommander.Server.UI.Pages.Servers.Components;
using LANCommander.Server.UI.Pages.Servers.Edit;
using Microsoft.Extensions.DependencyInjection;
using ServerModel = LANCommander.Server.Data.Models.Server;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>
/// The server editor's header as drawn: "Servers › {name}", the server's name and status, and the
/// "Unsaved changes" state that Discard clears. Nothing here saves, so the seeded server is untouched.
/// </summary>
[Collection("BUnit")]
public class ServerEditHeaderTests : BUnitTestContext
{
    private const string ServerName = "Test Server";
    private const string ServerHost = "10.0.0.1";

    private readonly Guid _serverId;

    public ServerEditHeaderTests(BUnitServerFixture fixture) : base(fixture)
    {
        _serverId = CreateServer();
    }

    private Guid CreateServer()
    {
        using var scope = Fixture.Factory.RealServices.CreateScope();
        var serverService = scope.ServiceProvider.GetRequiredService<ServerService>();

        var server = serverService.AddAsync(new ServerModel
        {
            Name = ServerName,
            Host = ServerHost,
            Engine = ServerEngine.Local,
        }).GetAwaiter().GetResult();

        return server.Id;
    }

    private IRenderedComponent<General> RenderGeneral(Guid id)
    {
        var cut = Render<General>(parameters => parameters.Add(p => p.Id, id));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".edit-form")), TimeSpan.FromSeconds(10));

        return cut;
    }

    private static AngleSharp.Dom.IElement InputFor(IRenderedComponent<General> cut, string label)
    {
        var id = cut.FindAll("label.lc-form-item-label")
            .First(l => l.TextContent.Trim() == label)
            .GetAttribute("for");

        return cut.Find($"#{id}");
    }

    [Fact]
    public void Header_ShowsBreadcrumbTitleAndStatus()
    {
        var cut = RenderGeneral(_serverId);

        var crumbs = cut.FindAll(".lc-page-header-breadcrumb li").Select(li => li.TextContent.Trim()).ToList();
        Assert.Equal(["Servers", ServerName], crumbs);

        Assert.Equal(ServerName, cut.Find(".lc-page-header-title").TextContent.Trim());

        // An unmanaged server reports Stopped, so its status tag settles deterministically.
        cut.WaitForAssertion(
            () => Assert.Equal("Stopped", cut.Find(".lc-page-header-tags .lc-tag").TextContent.Trim()),
            TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Header_ShowsUnsavedChanges_UntilDiscarded()
    {
        var cut = RenderGeneral(_serverId);

        Assert.Empty(cut.FindAll(".edit-unsaved"));
        Assert.True(cut.FindAll("button").Single(b => b.TextContent.Trim() == "Discard").HasAttribute("disabled"));

        InputFor(cut, "Host").Change("10.0.0.9");

        cut.WaitForAssertion(() => Assert.Contains("Unsaved changes", cut.Find(".edit-unsaved").TextContent));

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Discard").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll(".edit-unsaved"));
            Assert.NotEqual("10.0.0.9", InputFor(cut, "Host").GetAttribute("value"));
        }, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void AddNewServer_ShowsAddBreadcrumbAndTitle()
    {
        var cut = RenderGeneral(Guid.Empty);

        var crumbs = cut.FindAll(".lc-page-header-breadcrumb li").Select(li => li.TextContent.Trim()).ToList();
        Assert.Equal(["Servers", "Add server"], crumbs);

        Assert.Equal("Add new server", cut.Find(".lc-page-header-title").TextContent.Trim());

        // A blank new server starts clean: no unsaved indicator, Discard disabled.
        Assert.Empty(cut.FindAll(".edit-unsaved"));
        Assert.True(cut.FindAll("button").Single(b => b.TextContent.Trim() == "Discard").HasAttribute("disabled"));
    }
}

/// <summary>The snapshot behind the server editor's "Unsaved changes" indicator.</summary>
public class ServerEditSnapshotTests
{
    private static ServerModel NewServer() => new()
    {
        Name = "Arena Host",
        Host = "10.0.0.1",
        Port = 27015,
        Engine = ServerEngine.Local,
        Path = "/srv/game/run.sh",
        Autostart = true,
        AutostartDelay = 30,
    };

    [Fact]
    public void Snapshot_TreatsEmptyAndMissingTextAlike()
    {
        var server = NewServer();
        server.Arguments = "";
        var saved = ServerEditSnapshot.Of(server);

        server.Arguments = null!;

        Assert.Equal(saved, ServerEditSnapshot.Of(server));
    }

    [Fact]
    public void Snapshot_SeesEditedFields()
    {
        var server = NewServer();
        var saved = ServerEditSnapshot.Of(server);

        server.Port = 27016;
        Assert.NotEqual(saved, ServerEditSnapshot.Of(server));
        server.Port = 27015;

        server.Host = "10.0.0.2";
        Assert.NotEqual(saved, ServerEditSnapshot.Of(server));

        server = NewServer();
        saved = ServerEditSnapshot.Of(server);
        server.Autostart = false;
        Assert.NotEqual(saved, ServerEditSnapshot.Of(server));

        server = NewServer();
        saved = ServerEditSnapshot.Of(server);
        server.AutostartDelay = 60;
        Assert.NotEqual(saved, ServerEditSnapshot.Of(server));

        server = NewServer();
        saved = ServerEditSnapshot.Of(server);
        server.Engine = ServerEngine.Docker;
        Assert.NotEqual(saved, ServerEditSnapshot.Of(server));
    }
}
