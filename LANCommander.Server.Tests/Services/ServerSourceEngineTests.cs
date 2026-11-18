using LANCommander.Server.Plugins;
using LANCommander.Server.Services.Enums;
using LANCommander.Server.Services.Models;
using LANCommander.Server.Services.ServerSources;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace LANCommander.Server.Tests.Services;

/// <summary>
/// A plugin's <see cref="IServerSourceExtension"/> decides which servers there are: they are listed from
/// it, not the database, and answered for from the latest list.
/// </summary>
[Collection("Application")]
public class ServerSourceEngineTests : IAsyncDisposable
{
    private sealed class FakeSource(string id = "com.example.servers") : IServerSourceExtension
    {
        public string Id => id;
        public string Name => "Example";

        public IServerSourceHost? Host { get; private set; }
        public List<ExternalServer> Listed { get; set; } = [];
        public Exception? ListFailure { get; set; }
        public List<ExternalServer> Started { get; } = [];
        public List<ExternalServer> Stopped { get; } = [];
        public Exception? StartFailure { get; set; }

        public Task InitializeAsync(IServerSourceHost host, CancellationToken cancellationToken)
        {
            Host = host;

            return Task.CompletedTask;
        }

        public int ListCount;

        public Task<IReadOnlyList<ExternalServer>> GetServersAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref ListCount);

            if (ListFailure != null)
                throw ListFailure;

            return Task.FromResult<IReadOnlyList<ExternalServer>>(Listed.ToList());
        }

        public Task StartAsync(ExternalServer server, CancellationToken cancellationToken)
        {
            if (StartFailure != null)
                throw StartFailure;

            Started.Add(server);

            return Task.CompletedTask;
        }

        public Task StopAsync(ExternalServer server, CancellationToken cancellationToken)
        {
            Stopped.Add(server);

            return Task.CompletedTask;
        }

        public List<string> Commands { get; } = [];

        public Func<ExternalServer, IReadOnlyList<ExternalServerSection>>? Sections { get; set; }

        public IReadOnlyList<ExternalServerSection> GetSections(ExternalServer server) => Sections?.Invoke(server) ?? [];

        public Func<ExternalServer, Type?>? Overview { get; set; }

        public Type? GetOverview(ExternalServer server) => Overview?.Invoke(server);

        public Task<string?> SendCommandAsync(ExternalServer server, string command, CancellationToken cancellationToken)
        {
            Commands.Add(command);

            return Task.FromResult<string?>(command == "fail" ? throw new InvalidOperationException("Refused") : $"echo {command}");
        }

        public Task<ServerEngineProcessInfo?> GetProcessInfoAsync(ExternalServer server, CancellationToken cancellationToken) =>
            Task.FromResult<ServerEngineProcessInfo?>(new ServerEngineProcessInfo(42, null, 5, 2048));

        public async IAsyncEnumerable<ServerEngineLogLine> ReadLogAsync(ExternalServer server, ServerEngineLogRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();

            yield return new ServerEngineLogLine($"{server.Key} says hello");
        }
    }

    private readonly FakeSource _source = new();
    private readonly ServerSourceEngine _engine;
    private readonly List<ServerStatusUpdateEventArgs> _updates = [];

    public ServerSourceEngineTests()
    {
        _engine = new ServerSourceEngine(_source, NullLogger.Instance);
        _engine.OnServerStatusUpdate += (_, args) => _updates.Add(args);
    }

    public ValueTask DisposeAsync() => _engine.DisposeAsync();

    private static ExternalServer Server(string key, ServerEngineStatus status = ServerEngineStatus.Stopped, bool control = true) => new()
    {
        Key = key,
        Name = $"Server {key}",
        Host = "game.example",
        Port = 27015,
        Status = status,
        CanStart = control,
        CanStop = control,
    };

    private Guid IdOf(string key) => ExternalServerIds.Create(_source.Id, key);

    [Fact]
    public void IdsAreStableNameBasedAndScopedToTheSource()
    {
        var id = ExternalServerIds.Create("com.example.servers", "a");

        ExternalServerIds.Create("COM.Example.Servers", "a").ShouldBe(id);
        ExternalServerIds.Create("com.example.servers", "A").ShouldNotBe(id);
        ExternalServerIds.Create("com.example.other", "a").ShouldNotBe(id);

        // Version 5, RFC 4122 variant
        var text = id.ToString();
        text[14].ShouldBe('5');
        "89ab".ShouldContain(text[19]);
    }

    [Fact]
    public async Task ListedServersAreManagedFromTheLatestList()
    {
        _source.Listed = [Server("a", ServerEngineStatus.Running), Server("b")];

        await _engine.RefreshAsync();

        _engine.IsManaging(IdOf("a")).ShouldBeTrue();
        _engine.IsManaging(IdOf("b")).ShouldBeTrue();
        (await _engine.GetStatusAsync(IdOf("a"))).ShouldBe(ServerProcessStatus.Running);
        (await _engine.GetStatusAsync(IdOf("b"))).ShouldBe(ServerProcessStatus.Stopped);
        _engine.GetReportedAddress(IdOf("a")).ShouldBe(new ReportedServerAddress("game.example", 27015));

        _source.Listed = [Server("b")];

        await _engine.RefreshAsync();

        _engine.IsManaging(IdOf("a")).ShouldBeFalse();
        _engine.Servers.ShouldHaveSingleItem().Id.ShouldBe(IdOf("b"));
    }

    [Fact]
    public async Task ServersWithoutAKeyOrWithATakenOneAreLeftOut()
    {
        _source.Listed = [Server(""), Server("a") with { Name = "First" }, Server("a") with { Name = "Second" }];

        await _engine.RefreshAsync();

        _engine.Servers.ShouldHaveSingleItem().Server.Name.ShouldBe("First");
    }

    [Fact]
    public async Task StatusChangesAreAnnouncedButServersFoundAtStartAreNot()
    {
        _source.Listed = [Server("a", ServerEngineStatus.Running)];

        await _engine.RefreshAsync();

        _updates.ShouldBeEmpty();

        _source.Listed = [Server("a", ServerEngineStatus.Stopped), Server("b", ServerEngineStatus.Running)];

        await _engine.RefreshAsync();

        _updates.Select(u => (u.Server.Id, u.Status)).ShouldBe(
            [(IdOf("a"), ServerProcessStatus.Stopped), (IdOf("b"), ServerProcessStatus.Running)],
            ignoreOrder: true);
        _updates.ShouldAllBe(u => u.Server.Name.StartsWith("Server "));
    }

    [Fact]
    public async Task ServersAreKeptAsErroredWhileTheSourceCantListThem()
    {
        _source.Listed = [Server("a", ServerEngineStatus.Running)];

        await _engine.RefreshAsync();

        _source.ListFailure = new HttpRequestException("Connection refused");

        await _engine.RefreshAsync();

        var server = _engine.Find(IdOf("a")).ShouldNotBeNull();
        server.Status.ShouldBe(ServerProcessStatus.Error);
        server.StatusMessage.ShouldNotBeNull().ShouldContain("Connection refused");

        _source.ListFailure = null;

        await _engine.RefreshAsync();

        (await _engine.GetStatusAsync(IdOf("a"))).ShouldBe(ServerProcessStatus.Running);
    }

    [Fact]
    public async Task StartsAndStopsArePassedToTheSource()
    {
        _source.Listed = [Server("a")];

        await _engine.RefreshAsync();

        await _engine.StartAsync(IdOf("a"));

        _source.Started.ShouldHaveSingleItem().Key.ShouldBe("a");
        (await _engine.GetStatusAsync(IdOf("a"))).ShouldBe(ServerProcessStatus.Starting);

        await _engine.StopAsync(IdOf("a"));

        _source.Stopped.ShouldHaveSingleItem().Key.ShouldBe("a");
    }

    [Fact]
    public async Task ServersTheSourceDoesntLetBeControlledArentStarted()
    {
        _source.Listed = [Server("a", control: false)];

        await _engine.RefreshAsync();

        await Should.ThrowAsync<NotSupportedException>(() => _engine.StartAsync(IdOf("a")));
        await Should.ThrowAsync<NotSupportedException>(() => _engine.StopAsync(IdOf("a")));

        _source.Started.ShouldBeEmpty();
    }

    [Fact]
    public async Task AFailedStartLeavesTheServerErrored()
    {
        _source.Listed = [Server("a")];
        _source.StartFailure = new InvalidOperationException("No capacity");

        await _engine.RefreshAsync();

        await Should.ThrowAsync<InvalidOperationException>(() => _engine.StartAsync(IdOf("a")));

        var server = _engine.Find(IdOf("a")).ShouldNotBeNull();
        server.Status.ShouldBe(ServerProcessStatus.Error);
        server.StatusMessage.ShouldBe("No capacity");
    }

    [Fact]
    public async Task ReportedStatusesApplyAtOnce()
    {
        _source.Listed = [Server("a")];

        // Initializing starts listing in the background; let its first listing land, so it can't
        // replace what is reported below with what the source listed before
        await _engine.InitializeAsync();

        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (Volatile.Read(ref _source.ListCount) == 0 && DateTime.UtcNow < deadline)
            await Task.Delay(10);

        await Task.Delay(50);

        _source.Host.ShouldNotBeNull().ReportStatus("a", ServerEngineStatus.Running);
        _source.Host.ReportStatus("unknown", ServerEngineStatus.Running);

        (await _engine.GetStatusAsync(IdOf("a"))).ShouldBe(ServerProcessStatus.Running);
        _updates.ShouldContain(u => u.Server.Id == IdOf("a") && u.Status == ServerProcessStatus.Running);
    }

    [Fact]
    public async Task ASourceWhoseIdIsTakenIsLeftOut()
    {
        var first = new FakeSource("com.example.servers") { Listed = [Server("a")] };
        var second = new FakeSource("COM.EXAMPLE.SERVERS") { Listed = [Server("b")] };

        await using var registry = new ServerSourceRegistry([first, second], NullLoggerFactory.Instance);

        registry.GetEngines().Count().ShouldBe(1);

        await registry.RefreshAsync();

        registry.GetServers().ShouldHaveSingleItem().Server.Key.ShouldBe("a");
        registry.Find(ExternalServerIds.Create("com.example.servers", "a")).ShouldNotBeNull();
    }

    private const ExternalServerFeatures All = ExternalServerFeatures.Log | ExternalServerFeatures.Commands | ExternalServerFeatures.ProcessInfo;

    [Fact]
    public async Task CommandsGoToRunningServersWithTheFeature()
    {
        _source.Listed = [Server("on", ServerEngineStatus.Running) with { Features = All }, Server("off") with { Features = All }, Server("plain", ServerEngineStatus.Running)];

        await _engine.RefreshAsync();

        _engine.CanSendInput(IdOf("on")).ShouldBeTrue();
        _engine.CanSendInput(IdOf("off")).ShouldBeFalse();
        _engine.CanSendInput(IdOf("plain")).ShouldBeFalse();

        var sent = await _engine.SendCommandAsync(IdOf("on"), "status");
        sent.Sent.ShouldBeTrue();
        sent.Response.ShouldBe("echo status");

        var failed = await _engine.SendCommandAsync(IdOf("on"), "fail");
        failed.Sent.ShouldBeFalse();
        failed.Error.ShouldBe("Refused");

        (await _engine.SendCommandAsync(IdOf("plain"), "status")).Sent.ShouldBeFalse();
        _source.Commands.ShouldBe(["status", "fail"]);
    }

    [Fact]
    public async Task RunningServersWithTheFeatureAreSampled()
    {
        _source.Listed = [Server("on", ServerEngineStatus.Running) with { Features = All }, Server("off") with { Features = All }, Server("plain", ServerEngineStatus.Running)];

        await _engine.RefreshAsync();

        (await _engine.GetProcessInfoAsync(IdOf("on"))).ShouldBe(new ServerProcessInfo(42, null, 5, 2048));
        (await _engine.GetProcessInfoAsync(IdOf("off"))).ShouldBeNull();
        (await _engine.GetProcessInfoAsync(IdOf("plain"))).ShouldBeNull();
    }

    [Fact]
    public async Task OutputIsOnlyReadForServersWithTheFeature()
    {
        _source.Listed = [Server("on") with { Features = All }, Server("plain")];

        await _engine.RefreshAsync();

        _engine.CanReadLog(IdOf("on")).ShouldBeTrue();
        _engine.CanReadLog(IdOf("plain")).ShouldBeFalse();

        var lines = new List<string>();

        await foreach (var line in _engine.ReadLogAsync(IdOf("on"), new ServerEngineLogRequest(), CancellationToken.None))
            lines.Add(line.Text);

        lines.ShouldBe(["on says hello"]);

        Should.Throw<NotSupportedException>(() => _engine.ReadLogAsync(IdOf("plain"), new ServerEngineLogRequest(), CancellationToken.None));
    }

    private sealed class PageComponent : Microsoft.AspNetCore.Components.ComponentBase
    {
    }

    [Fact]
    public async Task OnlyPagesThatCanBeShownAreOffered()
    {
        _source.Listed = [Server("a"), Server("b")];
        _source.Sections = server => server.Key == "a"
            ?
            [
                new ExternalServerSection("players", "Players", typeof(PageComponent)),
                new ExternalServerSection("PLAYERS", "Again", typeof(PageComponent)),
                new ExternalServerSection("../escape", "Escape", typeof(PageComponent)),
                new ExternalServerSection("not-a-page", "Not a page", typeof(string)),
                new ExternalServerSection("config", "", typeof(PageComponent)),
            ]
            : [];

        await _engine.RefreshAsync();

        _engine.GetSections(IdOf("a")).Select(s => (s.Slug, s.Title)).ShouldBe([("players", "Players"), ("config", "config")]);
        _engine.GetSections(IdOf("b")).ShouldBeEmpty();
        _engine.GetSections(Guid.NewGuid()).ShouldBeEmpty();
    }

    [Fact]
    public async Task ASourceThatFailsToListItsPagesOffersNone()
    {
        _source.Listed = [Server("a")];
        _source.Sections = _ => throw new InvalidOperationException("Broken");

        await _engine.RefreshAsync();

        _engine.GetSections(IdOf("a")).ShouldBeEmpty();
    }

    [Fact]
    public async Task OnlyAnOverviewThatIsAComponentIsShown()
    {
        _source.Listed = [Server("page"), Server("text"), Server("none"), Server("broken")];
        _source.Overview = server => server.Key switch
        {
            "page" => typeof(PageComponent),
            "text" => typeof(string),
            "broken" => throw new InvalidOperationException("Broken"),
            _ => null,
        };

        await _engine.RefreshAsync();

        _engine.GetOverview(IdOf("page")).ShouldBe(typeof(PageComponent));
        _engine.GetOverview(IdOf("text")).ShouldBeNull();
        _engine.GetOverview(IdOf("none")).ShouldBeNull();
        _engine.GetOverview(IdOf("broken")).ShouldBeNull();
        _engine.GetOverview(Guid.NewGuid()).ShouldBeNull();
    }
}
