using Bunit;
using LANCommander.Server.Plugins;
using LANCommander.Server.Services.ServerSources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ExternalGeneral = LANCommander.Server.UI.Pages.Servers.External.General;
using ExternalMonitor = LANCommander.Server.UI.Pages.Servers.External.Monitor;
using ExternalSection = LANCommander.Server.UI.Pages.Servers.External.Section;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>
/// A plugin-listed server's General page, which shows what its source says about it, and its Monitor
/// page, which follows its output through its source.
/// </summary>
[Collection("BUnit")]
public class ExternalServerPagesTests : BUnitTestContext
{
    private sealed class FakeSource(params ExternalServer[] servers) : IServerSourceExtension
    {
        public string Id { get; } = $"com.example.{Guid.NewGuid():N}";
        public string Name => "Example Source";

        public Task<IReadOnlyList<ExternalServer>> GetServersAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ExternalServer>>(servers);

        public async IAsyncEnumerable<ServerEngineLogLine> ReadLogAsync(ExternalServer server, ServerEngineLogRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();

            yield return new ServerEngineLogLine("Map loaded: de_dust2");
        }

        public IReadOnlyList<ExternalServerSection> GetSections(ExternalServer server) =>
            [new ExternalServerSection("players", "Players", typeof(PlayersPage))];

        // Only servers with "summary" in their key have an overview
        public Type? GetOverview(ExternalServer server) => server.Key.Contains("summary") ? typeof(SummaryPanel) : null;
    }

    /// <summary>An overview a plugin adds to the General page, showing what it was given.</summary>
    public sealed class SummaryPanel : Microsoft.AspNetCore.Components.ComponentBase
    {
        [Microsoft.AspNetCore.Components.Parameter] public ExternalServer? Server { get; set; }
        [Microsoft.AspNetCore.Components.Parameter] public Guid ServerId { get; set; }

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "p");
            builder.AddAttribute(1, "class", "summary-panel");
            builder.AddContent(2, $"Summary of {Server?.Name} ({ServerId})");
            builder.CloseElement();
        }
    }

    /// <summary>A page a plugin adds, showing what it was given.</summary>
    public sealed class PlayersPage : Microsoft.AspNetCore.Components.ComponentBase
    {
        [Microsoft.AspNetCore.Components.Parameter] public ExternalServer? Server { get; set; }
        [Microsoft.AspNetCore.Components.Parameter] public Guid ServerId { get; set; }

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "p");
            builder.AddAttribute(1, "class", "players-page");
            builder.AddContent(2, $"Players on {Server?.Name} ({ServerId})");
            builder.CloseElement();
        }
    }

    private ServerSourceRegistry? _registry;

    public ExternalServerPagesTests(BUnitServerFixture fixture) : base(fixture)
    {
    }

    private async Task<Guid> ListAsync(ExternalServer server)
    {
        var source = new FakeSource(server);

        _registry = new ServerSourceRegistry([source], NullLoggerFactory.Instance);

        await _registry.RefreshAsync();

        Services.AddSingleton(_registry);

        return ExternalServerIds.Create(source.Id, server.Key);
    }

    private static ExternalServer Server(ExternalServerFeatures features = ExternalServerFeatures.None) => new()
    {
        Key = "dm",
        Name = "Linked Deathmatch",
        Host = "server-room.lan",
        Port = 27015,
        Location = "Server Room",
        ManageUrl = "http://server-room.lan:1337/Servers/1",
        CanStart = true,
        CanStop = true,
        Features = features,
    };

    [Fact]
    public async Task GeneralShowsWhatTheSourceSays()
    {
        var id = await ListAsync(Server());

        var cut = Render<ExternalGeneral>(parameters => parameters.Add(p => p.Id, id));

        cut.WaitForAssertion(() => Assert.Contains("Linked Deathmatch", cut.Find(".lc-page-header-title").TextContent), TimeSpan.FromSeconds(10));

        var values = cut.FindAll("input").Select(i => i.GetAttribute("value")).ToList();

        Assert.Contains("Example Source", values);
        Assert.Contains("Server Room", values);
        Assert.Contains("server-room.lan:27015", values);
        Assert.NotNull(cut.Find("a[href='http://server-room.lan:1337/Servers/1']"));

        // Nothing is kept about it here, so there's nothing to save
        Assert.DoesNotContain(cut.FindAll("button"), b => b.TextContent.Trim() == "Save");
    }

    [Fact]
    public async Task AServerNoSourceListsSaysSo()
    {
        await ListAsync(Server());

        var cut = Render<ExternalGeneral>(parameters => parameters.Add(p => p.Id, Guid.NewGuid()));

        cut.WaitForAssertion(() => Assert.Contains("No source lists this server", cut.Markup), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task MonitorFollowsOutputThroughTheSource()
    {
        var id = await ListAsync(Server(ExternalServerFeatures.Log));

        var cut = Render<ExternalMonitor>(parameters => parameters.Add(p => p.Id, id));

        cut.WaitForAssertion(() => Assert.Contains("Map loaded: de_dust2", cut.Markup), TimeSpan.FromSeconds(10));

        // The rail names the source in place of an engine
        Assert.Contains("Example Source", cut.Find(".server-monitor-facts").TextContent);
    }

    [Fact]
    public async Task MonitorSaysWhenTheSourceDoesntShareOutput()
    {
        var id = await ListAsync(Server());

        var cut = Render<ExternalMonitor>(parameters => parameters.Add(p => p.Id, id));

        cut.WaitForAssertion(() => Assert.Contains("doesn't share this server's output", cut.Markup), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task ASourcesPagesAreListedAndShownWithTheServer()
    {
        var id = await ListAsync(Server());

        var cut = Render<ExternalSection>(parameters => parameters
            .Add(p => p.Id, id)
            .Add(p => p.Slug, "players"));

        cut.WaitForAssertion(() => Assert.Equal($"Players on Linked Deathmatch ({id})", cut.Find(".players-page").TextContent), TimeSpan.FromSeconds(10));

        // Under the source's name in the menu; no Monitor for a source that shares nothing to monitor
        Assert.NotNull(cut.Find($"a[href='/Servers/External/{id}/Source/players']"));
        Assert.Contains("Example Source", cut.Markup);
        Assert.Empty(cut.FindAll($"a[href='/Servers/External/{id}/Monitor']"));
    }

    [Fact]
    public async Task APageTheSourceDoesntHaveSaysSo()
    {
        var id = await ListAsync(Server());

        var cut = Render<ExternalSection>(parameters => parameters
            .Add(p => p.Id, id)
            .Add(p => p.Slug, "missing"));

        cut.WaitForAssertion(() => Assert.Contains("has no such page for this server", cut.Markup), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task MonitorIsListedForASourceThatSharesOutput()
    {
        var id = await ListAsync(Server(ExternalServerFeatures.Log));

        var cut = Render<ExternalGeneral>(parameters => parameters.Add(p => p.Id, id));

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find($"a[href='/Servers/External/{id}/Monitor']")), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task GeneralShowsTheSourcesOverviewAboveItsOwnFields()
    {
        var id = await ListAsync(Server() with { Key = "dm-summary" });

        var cut = Render<ExternalGeneral>(parameters => parameters.Add(p => p.Id, id));

        cut.WaitForAssertion(() => Assert.Equal($"Summary of Linked Deathmatch ({id})", cut.Find(".summary-panel").TextContent), TimeSpan.FromSeconds(10));

        // Above the fields every listed server has
        Assert.True(cut.Markup.IndexOf("summary-panel", StringComparison.Ordinal) < cut.Markup.IndexOf(">Location<", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GeneralShowsNoOverviewWhenTheSourceHasNone()
    {
        var id = await ListAsync(Server());

        var cut = Render<ExternalGeneral>(parameters => parameters.Add(p => p.Id, id));

        cut.WaitForAssertion(() => Assert.Contains("Linked Deathmatch", cut.Find(".lc-page-header-title").TextContent), TimeSpan.FromSeconds(10));

        Assert.Empty(cut.FindAll(".external-server-overview"));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
            _registry?.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
