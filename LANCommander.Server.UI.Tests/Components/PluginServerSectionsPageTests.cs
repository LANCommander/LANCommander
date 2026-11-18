using Bunit;
using LANCommander.Server.Plugins;
using LANCommander.Server.Services;
using LANCommander.Server.Settings.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ServerGeneral = LANCommander.Server.UI.Pages.Servers.Edit.General;
using ServerPluginSection = LANCommander.Server.UI.Pages.Servers.Edit.PluginSection;
using ServerModel = LANCommander.Server.Data.Models.Server;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>
/// A plugin's pages for the server's own game servers: listed in a server's menu under the plugin's
/// name, shown with the server, and its summary at the top of the General page.
/// </summary>
[Collection("BUnit")]
public class PluginServerSectionsPageTests : BUnitTestContext
{
    private sealed class FakeExtension : IServerSectionExtension
    {
        public string Id => "com.acme.servers";
        public string Name => "Acme";

        public IReadOnlyList<ServerSection> GetSections(ServerInfo server) =>
            [new ServerSection("players", "Players", typeof(PlayersPage))];

        public Type? GetOverview(ServerInfo server) => typeof(SummaryPanel);
    }

    public sealed class PlayersPage : ComponentBase
    {
        [Parameter] public ServerInfo? Server { get; set; }
        [Parameter] public Guid ServerId { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "p");
            builder.AddAttribute(1, "class", "players-page");
            builder.AddContent(2, $"Players on {Server?.Name} ({ServerId})");
            builder.CloseElement();
        }
    }

    public sealed class SummaryPanel : ComponentBase
    {
        [Parameter] public ServerInfo? Server { get; set; }
        [Parameter] public Guid ServerId { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "p");
            builder.AddAttribute(1, "class", "summary-panel");
            builder.AddContent(2, $"Summary of {Server?.Name} on port {Server?.Port}");
            builder.CloseElement();
        }
    }

    private readonly Guid _serverId;
    private readonly string _serverName = $"Plugin Pages {Guid.NewGuid():N}";

    public PluginServerSectionsPageTests(BUnitServerFixture fixture) : base(fixture)
    {
        Services.AddSingleton(new PluginServerSections([new FakeExtension()], NullLogger<PluginServerSections>.Instance));

        using var scope = Fixture.Factory.RealServices.CreateScope();

        _serverId = scope.ServiceProvider.GetRequiredService<ServerService>().AddAsync(new ServerModel
        {
            Name = _serverName,
            Port = 27015,
            Engine = ServerEngine.Local,
        }).GetAwaiter().GetResult().Id;
    }

    [Fact]
    public void APluginsPageIsListedAndShownWithTheServer()
    {
        var cut = Render<ServerPluginSection>(parameters => parameters
            .Add(p => p.Id, _serverId)
            .Add(p => p.ExtensionId, "com.acme.servers")
            .Add(p => p.Slug, "players"));

        cut.WaitForAssertion(() => Assert.Equal($"Players on {_serverName} ({_serverId})", cut.Find(".players-page").TextContent), TimeSpan.FromSeconds(10));

        Assert.NotNull(cut.Find($"a[href='/Servers/{_serverId}/Plugin/com.acme.servers/players']"));
        Assert.Contains("Acme", cut.Markup);
    }

    [Fact]
    public void APageNoPluginOffersSaysSo()
    {
        var cut = Render<ServerPluginSection>(parameters => parameters
            .Add(p => p.Id, _serverId)
            .Add(p => p.ExtensionId, "com.acme.servers")
            .Add(p => p.Slug, "missing"));

        cut.WaitForAssertion(() => Assert.Contains("No plugin offers such a page for this server", cut.Markup), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void GeneralShowsAPluginsSummaryAboveTheSettings()
    {
        var cut = Render<ServerGeneral>(parameters => parameters.Add(p => p.Id, _serverId));

        cut.WaitForAssertion(() => Assert.Equal($"Summary of {_serverName} on port 27015", cut.Find(".summary-panel").TextContent), TimeSpan.FromSeconds(10));

        // Above the server's settings form
        var overview = cut.Markup.IndexOf("server-plugin-overview", StringComparison.Ordinal);
        var form = cut.Markup.IndexOf("<form", StringComparison.Ordinal);

        Assert.True(overview >= 0 && form > overview, $"The overview ({overview}) should come before the form ({form})");
    }
}
