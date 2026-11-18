using System.Net;
using System.Net.Http.Json;
using LANCommander.SDK.Enums;
using LANCommander.Server.Plugins;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Enums;
using LANCommander.Server.Services.Models;
using LANCommander.Server.Services.ServerEngines;
using LANCommander.Server.Settings.Enums;
using LANCommander.Server.Settings.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using ServerModel = LANCommander.Server.Data.Models.Server;
using ServerSettings = LANCommander.Server.Settings.Settings;

namespace LANCommander.Server.Tests.Services;

/// <summary>
/// Servers on another LANCommander server are run here as Remote servers: a linked host's servers are
/// added as they appear, filled in from it, then watched, started, stopped, monitored and sent
/// commands through the account the host was signed in with.
/// </summary>
[Collection("Application")]
public class RemoteServerEngineTests : DalTest, IAsyncDisposable
{
    private sealed class FakeHost : HttpMessageHandler
    {
        public Dictionary<Guid, ServerProcessStatus> Statuses { get; } = new();
        public List<SDK.Models.Server> Servers { get; } = new();
        public List<SequencedServerLogLine> Log { get; } = new();
        public List<string> Commands { get; } = new();
        public ServerProcessInfo? ProcessInfo { get; set; }
        public bool SupportsBulkStatus { get; set; } = true;
        public bool Down { get; set; }
        public bool Forbidden { get; set; }
        public List<string> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;

            lock (Requests)
                Requests.Add($"{request.Method} {path}");

            if (Down)
                throw new HttpRequestException("Connection refused");

            if (request.Headers.Authorization?.Parameter != "access")
                return Respond(HttpStatusCode.Unauthorized);

            if (Forbidden)
                return Respond(HttpStatusCode.Forbidden);

            if (request.Method == HttpMethod.Get && path == "/api/Server/")
                return Respond(HttpStatusCode.OK, Servers);

            if (request.Method == HttpMethod.Get && path == "/api/Server/Status")
                return SupportsBulkStatus ? Respond(HttpStatusCode.OK, Statuses) : Respond(HttpStatusCode.NotFound);

            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 4 || !Guid.TryParse(parts[2], out var id))
                return Respond(HttpStatusCode.NotFound);

            switch (request.Method.Method, parts[3])
            {
                case ("GET", "Status"):
                    return Respond(HttpStatusCode.OK, Statuses.GetValueOrDefault(id, ServerProcessStatus.Stopped));

                case ("POST", "Start"):
                    Statuses[id] = ServerProcessStatus.Running;
                    return Respond(HttpStatusCode.OK);

                case ("POST", "Stop"):
                    Statuses[id] = ServerProcessStatus.Stopped;
                    return Respond(HttpStatusCode.OK);

                case ("GET", "ProcessInfo"):
                    return Respond(HttpStatusCode.OK, ProcessInfo);

                case ("GET", "Log"):
                {
                    var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);
                    var after = long.Parse(query["after"] ?? "0");
                    var tail = int.Parse(query["tail"] ?? "1000");

                    lock (Log)
                        return Respond(HttpStatusCode.OK, new ServerLogPage(
                            Log.Where(l => l.Sequence > after).TakeLast(tail).ToList(),
                            Log.Count == 0 ? 0 : Log[^1].Sequence));
                }

                case ("POST", "Command"):
                {
                    var body = (await request.Content!.ReadFromJsonAsync<Dictionary<string, string>>(cancellationToken))!;

                    Commands.Add(body["text"]);

                    return Respond(HttpStatusCode.OK, body["text"] == "status"
                        ? new ServerCommandResult(true, "hostname: Deathmatch")
                        : ServerCommandResult.Failed("Unknown command"));
                }
            }

            return Respond(HttpStatusCode.NotFound);
        }

        private static HttpResponseMessage Respond(HttpStatusCode status, object? body = null) =>
            new(status) { Content = body == null ? null : JsonContent.Create(body) };
    }

    private sealed class StubOptionsMonitor<T>(T value) : Microsoft.Extensions.Options.IOptionsMonitor<T>
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"lc-remote-servers-{Guid.NewGuid():N}");
    private readonly ServerSettings _settings = new();
    private readonly FakeHost _remote = new();
    private readonly ServerEngineConfiguration _host;
    private readonly RemoteHostClient _client;
    private readonly RemoteServerEngine _engine;
    private readonly List<ServerStatusUpdateEventArgs> _updates = [];

    public RemoteServerEngineTests(ApplicationFixture fixture) : base(fixture)
    {
        Directory.CreateDirectory(_root);

        _host = new ServerEngineConfiguration
        {
            Name = "Server Room",
            Type = ServerEngine.Remote,
            Address = "http://server-room.lan:1337",
            AccessToken = "access",
            RefreshToken = "refresh",
            TokenExpiration = DateTime.UtcNow.AddHours(1),
        };

        _settings.Server.GameServers.ServerEngines = [_host];

        var settingsProvider = new SettingsProvider<ServerSettings>(new StubOptionsMonitor<ServerSettings>(_settings), Path.Combine(_root, "Settings.yml"));

        _client = new RemoteHostClient(NullLogger<RemoteHostClient>.Instance, settingsProvider, _remote);
        _engine = new RemoteServerEngine(NullLogger<RemoteServerEngine>.Instance, _client, ApplicationFixture.Instance.ServiceProvider);
        _engine.OnServerStatusUpdate += (_, args) => _updates.Add(args);

        RemoteServerEngine.LogPollInterval = TimeSpan.FromMilliseconds(20);
    }

    public async ValueTask DisposeAsync()
    {
        await _engine.DisposeAsync();
        _client.Dispose();
        Dispose();

        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    private SDK.Models.Server AddRemoteServer(string name, ServerProcessStatus status = ServerProcessStatus.Stopped, string host = "", int port = 27015, Guid? gameId = null)
    {
        var server = new SDK.Models.Server { Id = Guid.NewGuid(), Name = name, Host = host, Port = port, GameId = gameId };

        _remote.Servers.Add(server);
        _remote.Statuses[server.Id] = status;

        return server;
    }

    private async Task<List<ServerModel>> LocalServersAsync() =>
        (await GetService<ServerService>().AsNoTracking().GetAsync(s => s.Engine == ServerEngine.Remote && s.RemoteHostId == _host.Id)).ToList();

    private async Task<Guid> LocalIdOfAsync(SDK.Models.Server remote) =>
        (await LocalServersAsync()).Single(s => s.RemoteServerId == remote.Id).Id;

    // A server added here by hand, as before linked hosts added their own
    private async Task<ServerModel> AddLocallyAsync(SDK.Models.Server remote, Action<ServerModel>? configure = null)
    {
        var server = new ServerModel { Name = Unique("Mirror"), Engine = ServerEngine.Remote, RemoteHostId = _host.Id, RemoteServerId = remote.Id };

        configure?.Invoke(server);

        server = await GetService<ServerService>().AddAsync(server);

        await _engine.RefreshTrackingAsync();

        return server;
    }

    [Fact]
    public async Task ALinkedHostsServersAreAddedFilledInFromIt()
    {
        var game = await AddGameAsync();
        var deathmatch = AddRemoteServer("Deathmatch", gameId: game.Id);
        var coop = AddRemoteServer("Co-op", gameId: Guid.NewGuid());

        await _engine.PollAsync();

        var servers = await LocalServersAsync();

        servers.Count.ShouldBe(2);
        servers.Single(s => s.RemoteServerId == deathmatch.Id).ShouldSatisfyAllConditions(
            s => s.Name.ShouldBe("Deathmatch"),
            s => s.GameId.ShouldBe(game.Id),
            s => s.Host.ShouldBeEmpty(),
            s => s.Autostart.ShouldBeFalse());

        // Its game isn't one of this server's, so the administrator chooses it here
        servers.Single(s => s.RemoteServerId == coop.Id).GameId.ShouldBeNull();

        _host.AddedServerIds.ShouldBe([deathmatch.Id, coop.Id], ignoreOrder: true);
        servers.ShouldAllBe(s => _engine.IsManaging(s.Id));
    }

    [Fact]
    public async Task AServerIsAddedOnceAndNotAgainAfterItIsDeleted()
    {
        var deathmatch = AddRemoteServer("Deathmatch");

        await _engine.PollAsync();
        await _engine.PollAsync();

        var local = (await LocalServersAsync()).ShouldHaveSingleItem();

        await GetService<ServerService>().DeleteAsync(await GetService<ServerService>().GetAsync(local.Id));

        var coop = AddRemoteServer("Co-op");

        await _engine.PollAsync();

        (await LocalServersAsync()).ShouldHaveSingleItem().RemoteServerId.ShouldBe(coop.Id);
        _host.AddedServerIds.ShouldContain(deathmatch.Id);
    }

    [Fact]
    public async Task ServersAlreadyAddedByHandArentAddedTwice()
    {
        var deathmatch = AddRemoteServer("Deathmatch");
        var mine = await AddLocallyAsync(deathmatch, s => s.Name = "My Deathmatch");

        await _engine.PollAsync();

        var local = (await LocalServersAsync()).ShouldHaveSingleItem();
        local.Id.ShouldBe(mine.Id);
        local.Name.ShouldBe("My Deathmatch");
    }

    [Fact]
    public async Task HostsNotSetToAddServersOnlyRunThoseAddedByHand()
    {
        var deathmatch = AddRemoteServer("Deathmatch", ServerProcessStatus.Running);
        AddRemoteServer("Co-op");
        _host.AddServers = false;

        var mine = await AddLocallyAsync(deathmatch);

        await _engine.PollAsync();

        (await LocalServersAsync()).ShouldHaveSingleItem().Id.ShouldBe(mine.Id);
        (await _engine.GetStatusAsync(mine.Id)).ShouldBe(ServerProcessStatus.Running);
    }

    [Fact]
    public async Task StatusesComeFromTheLatestPollNotARequestEach()
    {
        var running = AddRemoteServer("Running", ServerProcessStatus.Running);
        var stopped = AddRemoteServer("Stopped");

        await _engine.PollAsync();

        var requests = _remote.Requests.Count;

        for (var i = 0; i < 10; i++)
        {
            (await _engine.GetStatusAsync(await LocalIdOfAsync(running))).ShouldBe(ServerProcessStatus.Running);
            (await _engine.GetStatusAsync(await LocalIdOfAsync(stopped))).ShouldBe(ServerProcessStatus.Stopped);
        }

        _remote.Requests.Count.ShouldBe(requests);
        _remote.Requests.ShouldContain("GET /api/Server/Status");
    }

    [Fact]
    public async Task StatusesAreAskedForOneByOneOfAnOlderHost()
    {
        var running = AddRemoteServer("Running", ServerProcessStatus.Running);
        _remote.SupportsBulkStatus = false;

        await _engine.PollAsync();

        (await _engine.GetStatusAsync(await LocalIdOfAsync(running))).ShouldBe(ServerProcessStatus.Running);
        _remote.Requests.ShouldContain($"GET /api/Server/{running.Id}/Status");
    }

    [Fact]
    public async Task StatusChangesOnTheHostAreAnnounced()
    {
        var deathmatch = AddRemoteServer("Deathmatch");

        await _engine.PollAsync();
        _updates.ShouldBeEmpty();

        _remote.Statuses[deathmatch.Id] = ServerProcessStatus.Running;

        await _engine.PollAsync();

        var update = _updates.ShouldHaveSingleItem();
        update.Status.ShouldBe(ServerProcessStatus.Running);
        update.Server.Name.ShouldBe("Deathmatch");
    }

    [Fact]
    public async Task AServerTheHostNoLongerHasIsErrored()
    {
        var deathmatch = AddRemoteServer("Deathmatch", ServerProcessStatus.Running);

        await _engine.PollAsync();

        var id = await LocalIdOfAsync(deathmatch);
        _remote.Servers.Clear();

        await _engine.PollAsync();

        (await _engine.GetStatusAsync(id)).ShouldBe(ServerProcessStatus.Error);
        _engine.GetProblem(id).ShouldNotBeNull().ShouldContain("no longer has this server");

        // Kept, so its game, autostart, actions and scripts aren't lost
        (await LocalServersAsync()).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task AnUnreachableHostsServersAreErroredButKeepTheirAddress()
    {
        var deathmatch = AddRemoteServer("Deathmatch", ServerProcessStatus.Running, host: "10.0.0.5", port: 7777);

        await _engine.PollAsync();

        var id = await LocalIdOfAsync(deathmatch);
        _remote.Down = true;

        await _engine.PollAsync();

        (await _engine.GetStatusAsync(id)).ShouldBe(ServerProcessStatus.Error);
        _engine.GetProblem(id).ShouldNotBeNull().ShouldContain("Server Room could not be reached");
        _engine.GetReportedAddress(id).ShouldBe(new ReportedServerAddress("10.0.0.5", 7777));
    }

    [Fact]
    public async Task AHostThatIsntConnectedSaysSoRatherThanLeavingItsServersWaiting()
    {
        var deathmatch = AddRemoteServer("Deathmatch");
        var mine = await AddLocallyAsync(deathmatch);

        _host.AccessToken = "";

        await _engine.PollAsync();

        (await _engine.GetStatusAsync(mine.Id)).ShouldBe(ServerProcessStatus.Error);
        _engine.GetProblem(mine.Id).ShouldNotBeNull().ShouldContain("hasn't been connected to");
        _remote.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task TheHostsAddressIsUsedWhereItLeavesTheServersBlank()
    {
        var deathmatch = AddRemoteServer("Deathmatch", port: 27015);

        await _engine.PollAsync();

        _engine.GetReportedAddress(await LocalIdOfAsync(deathmatch)).ShouldBe(new ReportedServerAddress("server-room.lan", 27015));
        _engine.GetManageUrl(await LocalIdOfAsync(deathmatch)).ShouldBe($"http://server-room.lan:1337/Servers/{deathmatch.Id}");
    }

    [Fact]
    public async Task StartsAndStopsGoToTheHost()
    {
        var deathmatch = AddRemoteServer("Deathmatch");

        await _engine.PollAsync();

        var id = await LocalIdOfAsync(deathmatch);

        await _engine.StartAsync(id);

        _remote.Requests.ShouldContain($"POST /api/Server/{deathmatch.Id}/Start");
        (await _engine.GetStatusAsync(id)).ShouldBe(ServerProcessStatus.Starting);

        await _engine.PollAsync();

        (await _engine.GetStatusAsync(id)).ShouldBe(ServerProcessStatus.Running);

        await _engine.StopAsync(id);

        _remote.Requests.ShouldContain($"POST /api/Server/{deathmatch.Id}/Stop");
    }

    [Fact]
    public async Task AServerSetToStartWithPlayerActivityStartsOnItsHost()
    {
        var game = await AddGameAsync();
        var deathmatch = AddRemoteServer("Deathmatch", gameId: game.Id);

        await _engine.PollAsync();

        var server = await GetService<ServerService>().GetAsync(await LocalIdOfAsync(deathmatch));
        server.Autostart = true;
        server.AutostartMethod = ServerAutostartMethod.OnPlayerActivity;
        await GetService<ServerService>().UpdateAsync(server);

        var manager = new ServerManager(
            NullLogger<ServerManager>.Instance,
            ApplicationFixture.Instance.ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
            [_engine],
            GetService<SettingsProvider<ServerSettings>>(),
            GetService<IRconCommandSender>());

        await manager.AutostartAsync(game.Id, ServerAutostartMethod.OnPlayerActivity);

        await Task.Delay(200);

        _remote.Requests.ShouldContain($"POST /api/Server/{deathmatch.Id}/Start");
    }

    [Fact]
    public async Task CommandsAreSentToRunningServersAndAnswered()
    {
        var running = AddRemoteServer("Running", ServerProcessStatus.Running);
        var stopped = AddRemoteServer("Stopped");

        await _engine.PollAsync();

        _engine.CanSendInput(await LocalIdOfAsync(running)).ShouldBeTrue();
        _engine.CanSendInput(await LocalIdOfAsync(stopped)).ShouldBeFalse();

        var answered = await _engine.SendCommandAsync(await LocalIdOfAsync(running), "status");
        answered.Sent.ShouldBeTrue();
        answered.Response.ShouldBe("hostname: Deathmatch");

        var refused = await _engine.SendCommandAsync(await LocalIdOfAsync(running), "nonsense");
        refused.Sent.ShouldBeFalse();
        refused.Error.ShouldBe("Unknown command");
    }

    [Fact]
    public async Task ProcessSamplesComeFromTheHost()
    {
        var running = AddRemoteServer("Running", ServerProcessStatus.Running);
        _remote.ProcessInfo = new ServerProcessInfo(4812, null, 12.5, 1024);

        await _engine.PollAsync();

        (await _engine.GetProcessInfoAsync(await LocalIdOfAsync(running))).ShouldBe(new ServerProcessInfo(4812, null, 12.5, 1024));
    }

    private void WriteRemoteLog(string message, ServerLogLevel level = ServerLogLevel.Info)
    {
        lock (_remote.Log)
            _remote.Log.Add(new SequencedServerLogLine(_remote.Log.Count + 1, new ServerLogLine(Guid.Empty, null, DateTime.UtcNow, level, message)));
    }

    [Fact]
    public async Task OutputIsReadBackThenFollowed()
    {
        var running = AddRemoteServer("Running", ServerProcessStatus.Running);

        await _engine.PollAsync();

        WriteRemoteLog("one");
        WriteRemoteLog("two", ServerLogLevel.Error);

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var read = new List<ServerEngineLogLine>();

        await foreach (var line in _engine.ReadLogAsync(await LocalIdOfAsync(running), new ServerEngineLogRequest(Tail: 10, Follow: true), cancellation.Token))
        {
            read.Add(line);

            if (read.Count == 2)
                WriteRemoteLog("three");

            if (read.Count == 3)
                break;
        }

        read.Select(l => (l.Text, l.IsError)).ShouldBe([("one", false), ("two", true), ("three", false)]);
    }

    [Fact]
    public async Task AnAccountThatIsntAnAdministratorThereIsExplained()
    {
        _remote.Forbidden = true;

        var error = await Should.ThrowAsync<HttpRequestException>(() => _client.GetServersAsync(_host.Id));

        error.Message.ShouldContain("isn't an administrator");
    }

    [Fact]
    public async Task BufferedOutputIsNumberedSoItCanBeFollowedFromElsewhere()
    {
        var serverId = Guid.NewGuid();
        var engine = new LoggingEngine();
        var manager = new ServerManager(
            NullLogger<ServerManager>.Instance,
            ApplicationFixture.Instance.ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
            [engine],
            GetService<SettingsProvider<ServerSettings>>(),
            GetService<IRconCommandSender>());

        await manager.InitializeAsync();

        engine.Write(serverId, "one");
        engine.Write(serverId, "two");
        engine.Write(serverId, "three");

        var all = manager.GetRecentLogAfter(serverId, 0);
        all.Last.ShouldBe(3);
        all.Lines.Select(l => (l.Sequence, l.Line.Message)).ShouldBe([(1L, "one"), (2L, "two"), (3L, "three")]);

        manager.GetRecentLogAfter(serverId, 2).Lines.ShouldHaveSingleItem().Line.Message.ShouldBe("three");
        manager.GetRecentLogAfter(serverId, 0, max: 1).Lines.ShouldHaveSingleItem().Line.Message.ShouldBe("three");

        // Clearing what is shown doesn't start the numbers again, so a follower isn't confused
        manager.ClearRecentLog(serverId);
        engine.Write(serverId, "four");

        manager.GetRecentLogAfter(serverId, 3).Lines.ShouldHaveSingleItem().Sequence.ShouldBe(4);
    }

    private sealed class LoggingEngine : IServerEngine
    {
        public event EventHandler<ServerStatusUpdateEventArgs>? OnServerStatusUpdate { add { } remove { } }
        public event EventHandler<ServerLogEventArgs>? OnServerLog;

        public Task InitializeAsync() => Task.CompletedTask;
        public Task RefreshTrackingAsync() => Task.CompletedTask;
        public bool IsManaging(Guid serverId) => false;
        public Task StartAsync(Guid serverId) => Task.CompletedTask;
        public Task StopAsync(Guid serverId) => Task.CompletedTask;
        public Task<ServerProcessStatus> GetStatusAsync(Guid serverId) => Task.FromResult(ServerProcessStatus.Stopped);

        public void Write(Guid serverId, string line) => OnServerLog?.Invoke(this, new ServerLogEventArgs(serverId, line));
    }
}
