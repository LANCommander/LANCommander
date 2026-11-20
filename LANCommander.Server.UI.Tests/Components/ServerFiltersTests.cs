using LANCommander.SDK.Enums;
using LANCommander.Server.Services.Enums;
using LANCommander.Server.Settings.Enums;
using LANCommander.Server.UI.Pages.Servers.Components;
using ServerModel = LANCommander.Server.Data.Models.Server;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>The Servers list's filters, in memory: no database needed.</summary>
public class ServerFiltersTests
{
    private static readonly ServerModel Running = new() { Id = Guid.NewGuid(), Name = "Running", Engine = ServerEngine.Local };
    private static readonly ServerModel Stopped = new() { Id = Guid.NewGuid(), Name = "Stopped", Engine = ServerEngine.Remote, Autostart = true, AutostartMethod = ServerAutostartMethod.OnPlayerActivity };
    private static readonly ServerModel Errored = new() { Id = Guid.NewGuid(), Name = "Errored", Engine = ServerEngine.Extension, EngineExtensionId = "com.example.docker", GameId = Guid.NewGuid() };

    /// <summary>A server not polled yet: it counts as stopped.</summary>
    private static readonly ServerModel Unpolled = new() { Id = Guid.NewGuid(), Name = "Unpolled" };

    private static readonly ServerModel[] All = [Running, Stopped, Errored, Unpolled];

    private static readonly ServerStatusSnapshot Statuses = new(new Dictionary<Guid, ServerProcessStatus>
    {
        [Running.Id] = ServerProcessStatus.Running,
        [Stopped.Id] = ServerProcessStatus.Stopped,
        [Errored.Id] = ServerProcessStatus.Error,
    });

    private static string[] Names(ServerFilters filters)
    {
        var predicate = filters.ToExpression(Statuses).Compile();

        return All.Where(predicate).Select(s => s.Name).ToArray();
    }

    [Theory]
    [InlineData("running", new[] { "Running" })]
    [InlineData("stopped", new[] { "Stopped", "Unpolled" })]
    [InlineData("errored", new[] { "Errored" })]
    [InlineData("autostart", new[] { "Stopped" })]
    [InlineData("no-game", new[] { "Running", "Stopped", "Unpolled" })]
    public void Views_MatchTheirServers(string view, string[] expected)
    {
        Assert.Equal(expected, Names(new ServerFilters { View = view }));
    }

    [Fact]
    public void StatusFacet_MatchesAnyChosenStatus()
    {
        var filters = new ServerFilters()
            .Toggle(ServerFacet.Status, nameof(ServerProcessStatus.Running))
            .Toggle(ServerFacet.Status, nameof(ServerProcessStatus.Error));

        Assert.Equal(["Running", "Errored"], Names(filters));
        Assert.True(filters.ByStatus);
    }

    [Fact]
    public void StatusFacet_StoppedIncludesServersNotPolledYet()
    {
        Assert.Equal(["Stopped", "Unpolled"], Names(new ServerFilters().Toggle(ServerFacet.Status, nameof(ServerProcessStatus.Stopped))));
    }

    [Fact]
    public void EngineFacet_TellsPluginEnginesApart()
    {
        var docker = new ServerFilters().Toggle(ServerFacet.Engine, ServerFilters.EngineKey(ServerEngine.Extension, "com.example.docker"));
        var other = new ServerFilters().Toggle(ServerFacet.Engine, ServerFilters.EngineKey(ServerEngine.Extension, "com.example.other"));
        var local = new ServerFilters().Toggle(ServerFacet.Engine, ServerFilters.EngineKey(ServerEngine.Local, null));

        Assert.Equal(["Errored"], Names(docker));
        Assert.Empty(Names(other));
        Assert.Equal(["Running", "Unpolled"], Names(local));
    }

    [Fact]
    public void AutostartFacet_MatchesMethodsAndOff()
    {
        var onActivity = new ServerFilters().Toggle(ServerFacet.Autostart, nameof(ServerAutostartMethod.OnPlayerActivity));
        var off = new ServerFilters().Toggle(ServerFacet.Autostart, ServerFilters.AutostartOff);

        Assert.Equal(["Stopped"], Names(onActivity));
        Assert.Equal(["Running", "Errored", "Unpolled"], Names(off));
    }

    [Fact]
    public void Facets_CombineWithTheView()
    {
        var filters = new ServerFilters { View = "stopped" }.Toggle(ServerFacet.Engine, nameof(ServerEngine.Remote));

        Assert.Equal(["Stopped"], Names(filters));
    }

    [Fact]
    public void ChoosingAFacet_LeavesTheExternalView()
    {
        var filters = new ServerFilters { View = ServerFilters.External }.Toggle(ServerFacet.Engine, nameof(ServerEngine.Local));

        Assert.Equal(ServerFilters.AllServers, filters.View);
    }

    [Fact]
    public void SavedViews_RoundTrip()
    {
        var filters = new ServerFilters { View = "running" }.Toggle(ServerFacet.Engine, nameof(ServerEngine.Local));
        var restored = SavedServerView.From("Mine", filters).ToFilters();

        Assert.Equal(filters.View, restored.View);
        Assert.True(restored.Has(ServerFacet.Engine, nameof(ServerEngine.Local)));
    }
}
