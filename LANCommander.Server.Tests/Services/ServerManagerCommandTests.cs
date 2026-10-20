using LANCommander.SDK.Enums;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Enums;
using LANCommander.Server.Services.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using DataServerConsole = LANCommander.Server.Data.Models.ServerConsole;
using ServerSettings = LANCommander.Server.Settings.Settings;

namespace LANCommander.Server.Tests.Services;

/// <summary>
/// <see cref="ServerManager.SendCommandAsync"/> routes RCON consoles to RCON and everything else
/// (no console, or a log file console) to the managing engine's standard input, and
/// <see cref="ServerManager.CanSendCommandAsync"/> agrees with it.
/// </summary>
[Collection("Application")]
public class ServerManagerCommandTests(ApplicationFixture fixture) : DalTest(fixture)
{
    private sealed class FakeEngine(Guid managedServerId) : IServerEngine
    {
        public bool InputAvailable { get; set; } = true;
        public List<string> Input { get; } = new();

        public event EventHandler<ServerStatusUpdateEventArgs>? OnServerStatusUpdate;
        public event EventHandler<ServerLogEventArgs>? OnServerLog;

        public Task InitializeAsync() => Task.CompletedTask;
        public Task RefreshTrackingAsync() => Task.CompletedTask;
        public bool IsManaging(Guid serverId) => serverId == managedServerId;
        public Task StartAsync(Guid serverId) => Task.CompletedTask;
        public Task StopAsync(Guid serverId) => Task.CompletedTask;
        public Task<ServerProcessStatus> GetStatusAsync(Guid serverId) => Task.FromResult(ServerProcessStatus.Running);

        public bool CanSendInput(Guid serverId) => IsManaging(serverId) && InputAvailable;

        public Task<bool> SendInputAsync(Guid serverId, string line)
        {
            if (!CanSendInput(serverId))
                return Task.FromResult(false);

            Input.Add(line);

            return Task.FromResult(true);
        }

        public void RaiseLog(ServerLogEventArgs args) => OnServerLog?.Invoke(this, args);
    }

    private sealed class FakeRcon : IRconCommandSender
    {
        public List<(Guid ConsoleId, string Command)> Sent { get; } = new();

        public bool CanSend(DataServerConsole console) =>
            console.Type == ServerConsoleType.RCON && console.Port.HasValue && !String.IsNullOrWhiteSpace(console.Host);

        public Task<string> SendCommandAsync(DataServerConsole console, string command, CancellationToken cancellationToken = default)
        {
            Sent.Add((console.Id, command));

            return Task.FromResult($"ok: {command}");
        }
    }

    private ServerManager CreateManager(IServerEngine engine, IRconCommandSender rcon) =>
        new(
            NullLogger<ServerManager>.Instance,
            ApplicationFixture.Instance.ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
            [engine],
            GetService<SettingsProvider<ServerSettings>>(),
            rcon);

    private Task<DataServerConsole> AddConsoleAsync(Guid serverId, ServerConsoleType type, string host = "127.0.0.1", int? port = 27015) =>
        GetService<ServerConsoleService>().AddAsync(new DataServerConsole
        {
            Name = Unique("Console"),
            Type = type,
            Host = host,
            Port = port,
            Password = "secret",
            Path = "logs/server.log",
            ServerId = serverId,
        });

    [Fact]
    public async Task CommandWithoutConsoleGoesToStandardInput()
    {
        var server = await AddServerAsync();
        var engine = new FakeEngine(server.Id);
        var rcon = new FakeRcon();
        var manager = CreateManager(engine, rcon);

        (await manager.CanSendCommandAsync(server.Id, null)).ShouldBeTrue();

        var result = await manager.SendCommandAsync(server.Id, null, "say hello");

        result.Sent.ShouldBeTrue();
        engine.Input.ShouldBe(["say hello"]);
        rcon.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task CommandToRconConsoleGoesToRcon()
    {
        var server = await AddServerAsync();
        var console = await AddConsoleAsync(server.Id, ServerConsoleType.RCON);
        var engine = new FakeEngine(server.Id);
        var rcon = new FakeRcon();
        var manager = CreateManager(engine, rcon);

        (await manager.CanSendCommandAsync(server.Id, console.Id)).ShouldBeTrue();

        var result = await manager.SendCommandAsync(server.Id, console.Id, "status");

        result.Sent.ShouldBeTrue();
        result.Response.ShouldBe("ok: status");
        rcon.Sent.ShouldBe([(console.Id, "status")]);
        engine.Input.ShouldBeEmpty();
    }

    [Fact]
    public async Task RconConsoleWithoutPortCannotSend()
    {
        var server = await AddServerAsync();
        var console = await AddConsoleAsync(server.Id, ServerConsoleType.RCON, port: null);
        var rcon = new FakeRcon();
        var manager = CreateManager(new FakeEngine(server.Id), rcon);

        (await manager.CanSendCommandAsync(server.Id, console.Id)).ShouldBeFalse();

        var result = await manager.SendCommandAsync(server.Id, console.Id, "status");

        result.Sent.ShouldBeFalse();
        result.Error.ShouldNotBeNullOrWhiteSpace();
        rcon.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task LogFileConsoleUsesStandardInputWhenAvailable()
    {
        var server = await AddServerAsync();
        var console = await AddConsoleAsync(server.Id, ServerConsoleType.LogFile);
        var engine = new FakeEngine(server.Id);
        var manager = CreateManager(engine, new FakeRcon());

        (await manager.CanSendCommandAsync(server.Id, console.Id)).ShouldBeTrue();

        (await manager.SendCommandAsync(server.Id, console.Id, "restart")).Sent.ShouldBeTrue();
        engine.Input.ShouldBe(["restart"]);
    }

    [Fact]
    public async Task ShellExecuteServersCannotReceiveCommands()
    {
        var server = await AddServerAsync();
        var console = await AddConsoleAsync(server.Id, ServerConsoleType.LogFile);
        var engine = new FakeEngine(server.Id) { InputAvailable = false };
        var manager = CreateManager(engine, new FakeRcon());

        (await manager.CanSendCommandAsync(server.Id, null)).ShouldBeFalse();
        (await manager.CanSendCommandAsync(server.Id, console.Id)).ShouldBeFalse();

        var result = await manager.SendCommandAsync(server.Id, console.Id, "restart");

        result.Sent.ShouldBeFalse();
        engine.Input.ShouldBeEmpty();
    }

    [Fact]
    public async Task ConsoleOfAnotherServerIsRejected()
    {
        var server = await AddServerAsync();
        var otherServer = await AddServerAsync();
        var otherConsole = await AddConsoleAsync(otherServer.Id, ServerConsoleType.RCON);
        var rcon = new FakeRcon();
        var manager = CreateManager(new FakeEngine(server.Id), rcon);

        (await manager.CanSendCommandAsync(server.Id, otherConsole.Id)).ShouldBeFalse();
        (await manager.SendCommandAsync(server.Id, otherConsole.Id, "status")).Sent.ShouldBeFalse();
        rcon.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task UnmanagedServerOrEmptyCommandIsNotSent()
    {
        var engine = new FakeEngine(Guid.NewGuid());
        var manager = CreateManager(engine, new FakeRcon());

        (await manager.CanSendCommandAsync(Guid.NewGuid(), null)).ShouldBeFalse();
        (await manager.SendCommandAsync(Guid.NewGuid(), null, "status")).Sent.ShouldBeFalse();
        (await manager.SendCommandAsync(Guid.NewGuid(), null, "   ")).Sent.ShouldBeFalse();
        engine.Input.ShouldBeEmpty();
    }

    [Fact]
    public async Task EngineOutputIsBufferedAndRelayed()
    {
        var serverId = Guid.NewGuid();
        var engine = new FakeEngine(serverId);
        var manager = CreateManager(engine, new FakeRcon());

        await manager.InitializeAsync();

        var relayed = new List<ServerLogEventArgs>();
        manager.OnServerLog += (_, args) => relayed.Add(args);

        engine.RaiseLog(new ServerLogEventArgs(serverId, "Server started on port 27015"));
        engine.RaiseLog(new ServerLogEventArgs(serverId, "ERROR: map not found"));

        relayed.Count.ShouldBe(2);

        var lines = manager.GetRecentLog(serverId);

        lines.Select(l => l.Message).ShouldBe(["Server started on port 27015", "ERROR: map not found"]);
        lines.Select(l => l.Level).ShouldBe([ServerLogLevel.Ready, ServerLogLevel.Error]);
        lines.ShouldAllBe(l => l.ServerId == serverId && l.ConsoleId == null);

        manager.ClearRecentLog(serverId);
        manager.GetRecentLog(serverId).ShouldBeEmpty();
    }
}
