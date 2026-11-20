using Bunit;
using LANCommander.Server.Plugins;
using LANCommander.Server.Services.ServerSources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ServersIndex = LANCommander.Server.UI.Pages.Servers.Index;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>
/// Servers listed by sources, such as a linked LANCommander server, appear on the Servers page below
/// those added here, with where they come from and a link to where they are managed.
/// </summary>
[Collection("BUnit")]
public class ExternalServersListTests : BUnitTestContext
{
    private sealed class FakeSource(params ExternalServer[] servers) : IServerSourceExtension
    {
        public string Id => "com.example.servers";
        public string Name => "Example Source";

        public Task<IReadOnlyList<ExternalServer>> GetServersAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ExternalServer>>(servers);
    }

    public ExternalServersListTests(BUnitServerFixture fixture) : base(fixture)
    {
    }

    private async Task<ServerSourceRegistry> UseSourceAsync(params ExternalServer[] servers)
    {
        var registry = new ServerSourceRegistry([new FakeSource(servers)], NullLoggerFactory.Instance);

        await registry.RefreshAsync();

        Services.AddSingleton(registry);

        return registry;
    }

    [Fact]
    public async Task ListedServersAreShownWithTheirSourceAndManageLink()
    {
        await using var registry = await UseSourceAsync(new ExternalServer
        {
            Key = "dm",
            Name = "Linked Deathmatch",
            Host = "server-room.lan",
            Port = 27015,
            Location = "Server Room",
            ManageUrl = "http://server-room.lan:1337/Servers/1",
            CanStart = true,
            CanStop = true,
        });

        var cut = Render<ServersIndex>();

        cut.WaitForAssertion(() => Assert.Contains("Linked Deathmatch", cut.Markup), TimeSpan.FromSeconds(10));

        Assert.Contains("External Servers", cut.Markup);
        Assert.Contains("Example Source", cut.Markup);
        Assert.Contains("Server Room", cut.Markup);
        Assert.Contains("server-room.lan:27015", cut.Markup);

        var manage = cut.Find("a[href='http://server-room.lan:1337/Servers/1']");
        Assert.Equal("_blank", manage.GetAttribute("target"));
    }

    [Fact]
    public async Task TheRailOffersStatusViewsAndAnExternalView()
    {
        await using var registry = await UseSourceAsync(new ExternalServer { Key = "dm", Name = "Linked Deathmatch" });

        var cut = Render<ServersIndex>();

        cut.WaitForAssertion(() => Assert.Contains("Linked Deathmatch", cut.Markup), TimeSpan.FromSeconds(10));

        var views = cut.FindAll(".filter-rail-view").Select(v => v.QuerySelector(".filter-rail-name")!.TextContent).ToList();

        Assert.Contains("All servers", views);
        Assert.Contains("Running", views);
        Assert.Contains("Stopped", views);
        Assert.Contains("Errored", views);
        Assert.Contains("External", views);

        // External shows the listed servers on their own, without the server's own list
        cut.FindAll(".filter-rail-view").Single(v => v.TextContent.Contains("External")).Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Linked Deathmatch", cut.Markup);
            Assert.DoesNotContain("External Servers", cut.Markup);
            Assert.Contains("1 external", cut.Markup);
        });
    }

    [Fact]
    public async Task TheSectionIsHiddenWhenNothingIsListed()
    {
        await using var registry = await UseSourceAsync();

        var cut = Render<ServersIndex>();

        cut.WaitForAssertion(() => Assert.Contains("Add Server", cut.Markup), TimeSpan.FromSeconds(10));

        Assert.DoesNotContain("External Servers", cut.Markup);
    }
}
