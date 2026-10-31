using System.Collections.Concurrent;
using LANCommander.Server.Plugins;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Enums;
using LANCommander.Server.Services.Models;
using LANCommander.Server.Settings.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using ServerModel = LANCommander.Server.Data.Models.Server;
using ServerSettings = LANCommander.Server.Settings.Settings;

namespace LANCommander.Server.Tests.Services;

/// <summary>
/// A plugin's <see cref="IServerEngineExtension"/> runs the servers saved with its id, through the
/// same <see cref="ServerManager"/> as the built-in engines, and sees each server with its settings.
/// </summary>
[Collection("Application")]
public class PluginServerEngineTests(ApplicationFixture fixture) : DalTest(fixture)
{
    private sealed class FakeExtension(string id, ServerEngineFeatures features = ServerEngineFeatures.None) : IServerEngineExtension
    {
        public string Id => id;
        public string Name => "Fake";
        public ServerEngineFeatures Features => features;

        public IServerEngineHost? Host { get; private set; }
        public IReadOnlyList<ServerEngineServer> Servers { get; private set; } = [];
        public ConcurrentDictionary<Guid, ServerEngineStatus> Status { get; } = new();
        public List<ServerEngineServer> Started { get; } = new();
        public List<string> Input { get; } = new();
        public Exception? StartFailure { get; set; }
        public Exception? StatusFailure { get; set; }

        public Task InitializeAsync(IServerEngineHost host, CancellationToken cancellationToken)
        {
            Host = host;

            return Task.CompletedTask;
        }

        public Task ServersChangedAsync(IReadOnlyList<ServerEngineServer> servers, CancellationToken cancellationToken)
        {
            Servers = servers;

            return Task.CompletedTask;
        }

        public Task StartAsync(ServerEngineServer server, CancellationToken cancellationToken)
        {
            if (StartFailure != null)
                throw StartFailure;

            Started.Add(server);
            Status[server.Id] = ServerEngineStatus.Running;

            return Task.CompletedTask;
        }

        public Task StopAsync(ServerEngineServer server, CancellationToken cancellationToken)
        {
            Status[server.Id] = ServerEngineStatus.Stopped;

            return Task.CompletedTask;
        }

        public Task<ServerEngineStatus> GetStatusAsync(ServerEngineServer server, CancellationToken cancellationToken)
        {
            if (StatusFailure != null)
                throw StatusFailure;

            return Task.FromResult(Status.GetValueOrDefault(server.Id, ServerEngineStatus.Stopped));
        }

        public Task<ServerEngineProcessInfo?> GetProcessInfoAsync(ServerEngineServer server, CancellationToken cancellationToken)
            => Task.FromResult<ServerEngineProcessInfo?>(new ServerEngineProcessInfo(null, null, 12.5, 1024));

        public bool CanSendInput(ServerEngineServer server) => true;

        public Task<bool> SendInputAsync(ServerEngineServer server, string line, CancellationToken cancellationToken)
        {
            Input.Add(line);

            return Task.FromResult(true);
        }

        public IReadOnlyList<ServerEngineConsole> Consoles { get; set; } =
        [
            new("logs", "Logs", ServerEngineConsoleKind.Log),
            new("Shell", "Shell", ServerEngineConsoleKind.Terminal),
        ];

        public List<(string ConsoleId, ServerEngineTerminalSize Size)> Opened { get; } = new();

        public IReadOnlyList<ServerEngineConsole> GetConsoles(ServerEngineServer server) => Consoles;

        public async IAsyncEnumerable<ServerEngineLogLine> ReadLogAsync(ServerEngineServer server, string consoleId, ServerEngineLogRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();

            yield return new ServerEngineLogLine($"{consoleId} of {server.GetSettings<Settings>().ContainerId}, last {request.Tail}");
        }

        public Task<IServerEngineTerminal> OpenTerminalAsync(ServerEngineServer server, string consoleId, ServerEngineTerminalSize size, CancellationToken cancellationToken)
        {
            Opened.Add((consoleId, size));

            return Task.FromResult<IServerEngineTerminal>(new EchoTerminal());
        }

        /// <summary>The server's files by path, and every path the engine was asked about.</summary>
        public ConcurrentDictionary<string, string> Files { get; } = new();
        public List<string> FilePaths { get; } = new();

        public Task<IReadOnlyList<ServerEngineFile>> ListFilesAsync(ServerEngineServer server, string path, CancellationToken cancellationToken)
        {
            FilePaths.Add(path);

            IReadOnlyList<ServerEngineFile> files =
            [
                .. Files.Keys.Where(key => key.StartsWith(path) && !key[path.Length..].TrimStart('/').Contains('/'))
                    .Select(key => new ServerEngineFile(key[path.Length..].TrimStart('/'), false, Files[key].Length)),
                // Nothing that isn't a name within the folder is passed on
                new ServerEngineFile("../escape", false),
                new ServerEngineFile("", false),
            ];

            return Task.FromResult(files);
        }

        public Task<Stream> OpenFileAsync(ServerEngineServer server, string path, CancellationToken cancellationToken)
        {
            FilePaths.Add(path);

            return Task.FromResult<Stream>(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(Files[path])));
        }

        public async Task WriteFileAsync(ServerEngineServer server, string path, Stream content, CancellationToken cancellationToken)
        {
            FilePaths.Add(path);

            Files[path] = await new StreamReader(content).ReadToEndAsync(cancellationToken);
        }

        public Task DeleteFileAsync(ServerEngineServer server, string path, CancellationToken cancellationToken)
        {
            FilePaths.Add(path);
            Files.TryRemove(path, out _);

            return Task.CompletedTask;
        }
    }

    /// <summary>Writes back whatever is typed into it.</summary>
    private sealed class EchoTerminal : IServerEngineTerminal
    {
        private readonly System.Threading.Channels.Channel<byte[]> _output = System.Threading.Channels.Channel.CreateUnbounded<byte[]>();

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (!await _output.Reader.WaitToReadAsync(cancellationToken))
                return 0;

            var bytes = await _output.Reader.ReadAsync(cancellationToken);

            bytes.CopyTo(buffer);

            return bytes.Length;
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> input, CancellationToken cancellationToken) =>
            _output.Writer.WriteAsync(input.ToArray(), cancellationToken);

        public ValueTask DisposeAsync()
        {
            _output.Writer.TryComplete();

            return ValueTask.CompletedTask;
        }
    }

    private sealed class Rcon : IRconCommandSender
    {
        public bool CanSend(LANCommander.Server.Data.Models.ServerConsole console) => false;

        public Task<string> SendCommandAsync(LANCommander.Server.Data.Models.ServerConsole console, string command, CancellationToken cancellationToken = default)
            => Task.FromResult("");
    }

    private static string NewEngineId() => $"test.engine.{Guid.NewGuid():N}";

    private static PluginServerEngines CreateEngines(params IServerEngineExtension[] extensions) =>
        new(extensions, ApplicationFixture.Instance.ServiceProvider, NullLogger<PluginServerEngines>.Instance);

    private async Task<ServerManager> CreateManagerAsync(PluginServerEngines engines)
    {
        var manager = new ServerManager(
            NullLogger<ServerManager>.Instance,
            ApplicationFixture.Instance.ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
            [],
            GetService<SettingsProvider<ServerSettings>>(),
            new Rcon(),
            [engines]);

        await manager.InitializeAsync();

        return manager;
    }

    private Task<ServerModel> AddServerAsync(string engineId, string settings = "") =>
        GetService<ServerService>().AddAsync(new ServerModel
        {
            Name = Unique("Server"),
            Host = "10.0.0.5",
            Port = 27015,
            Engine = ServerEngine.Extension,
            EngineExtensionId = engineId,
            EngineSettings = settings,
        });

    [Fact]
    public async Task TheEngineRunsOnlyTheServersSavedWithItsId()
    {
        var extension = new FakeExtension(NewEngineId());
        var mine = await AddServerAsync(extension.Id, """{"containerId":"abc"}""");
        var others = await AddServerAsync(NewEngineId());
        var local = await GetService<ServerService>().AddAsync(new ServerModel { Name = Unique("Local"), Engine = ServerEngine.Local });

        var manager = await CreateManagerAsync(CreateEngines(extension));

        manager.IsManaging(mine.Id).ShouldBeTrue();
        manager.IsManaging(others.Id).ShouldBeFalse();
        manager.IsManaging(local.Id).ShouldBeFalse();

        var server = extension.Servers.ShouldHaveSingleItem();
        server.Id.ShouldBe(mine.Id);
        server.Host.ShouldBe("10.0.0.5");
        server.Port.ShouldBe(27015);
        server.Settings.ShouldBe("""{"containerId":"abc"}""");
    }

    [Fact]
    public async Task StartingAndStoppingGoThroughTheEngineAndAreReported()
    {
        var extension = new FakeExtension(NewEngineId());
        var server = await AddServerAsync(extension.Id, """{"containerId":"abc"}""");
        var engines = CreateEngines(extension);
        var manager = await CreateManagerAsync(engines);
        var statuses = new List<ServerProcessStatus>();

        foreach (var engine in manager.Engines)
            engine.OnServerStatusUpdate += (_, args) => statuses.Add(args.Status);

        await manager.StartAsync(server.Id);

        extension.Started.ShouldHaveSingleItem().GetSettings<Settings>().ContainerId.ShouldBe("abc");
        (await manager.GetStatusAsync(server.Id)).ShouldBe(ServerProcessStatus.Running);

        await manager.StopAsync(server.Id);

        (await manager.GetStatusAsync(server.Id)).ShouldBe(ServerProcessStatus.Stopped);
        statuses.ShouldBe([ServerProcessStatus.Starting, ServerProcessStatus.Running, ServerProcessStatus.Stopping, ServerProcessStatus.Stopped]);
    }

    [Fact]
    public async Task AFailedStartIsReportedAndThrown()
    {
        var extension = new FakeExtension(NewEngineId()) { StartFailure = new InvalidOperationException("No such container") };
        var server = await AddServerAsync(extension.Id);
        var manager = await CreateManagerAsync(CreateEngines(extension));
        var statuses = new List<ServerProcessStatus>();

        foreach (var engine in manager.Engines)
            engine.OnServerStatusUpdate += (_, args) => statuses.Add(args.Status);

        (await Should.ThrowAsync<InvalidOperationException>(() => manager.StartAsync(server.Id))).Message.ShouldBe("No such container");

        statuses.ShouldBe([ServerProcessStatus.Starting, ServerProcessStatus.Error]);
    }

    [Fact]
    public async Task AnEngineThatCannotTellItsStatusShowsAnError()
    {
        var extension = new FakeExtension(NewEngineId()) { StatusFailure = new HttpRequestException("Host unreachable") };
        var server = await AddServerAsync(extension.Id);
        var manager = await CreateManagerAsync(CreateEngines(extension));

        (await manager.GetStatusAsync(server.Id)).ShouldBe(ServerProcessStatus.Error);
    }

    [Fact]
    public async Task InputAndSamplesNeedTheirFeatures()
    {
        var without = new FakeExtension(NewEngineId());
        var with = new FakeExtension(NewEngineId(), ServerEngineFeatures.Input | ServerEngineFeatures.ProcessInfo);
        var plain = await AddServerAsync(without.Id);
        var featured = await AddServerAsync(with.Id);
        var manager = await CreateManagerAsync(CreateEngines(without, with));

        (await manager.CanSendCommandAsync(plain.Id)).ShouldBeFalse();
        (await manager.GetProcessInfoAsync(plain.Id)).ShouldBeNull();

        (await manager.CanSendCommandAsync(featured.Id)).ShouldBeTrue();
        (await manager.SendCommandAsync(featured.Id, null, "say hi")).Sent.ShouldBeTrue();
        with.Input.ShouldBe(["say hi"]);
        (await manager.GetProcessInfoAsync(featured.Id)).ShouldBe(new ServerProcessInfo(null, null, 12.5, 1024));
    }

    [Fact]
    public async Task OutputTheEngineReportsReachesTheServersLog()
    {
        var extension = new FakeExtension(NewEngineId(), ServerEngineFeatures.Output);
        var server = await AddServerAsync(extension.Id);
        var manager = await CreateManagerAsync(CreateEngines(extension));

        extension.Host!.WriteOutput(server.Id, "Map loaded");
        extension.Host.WriteOutput(Guid.NewGuid(), "Not one of its servers");

        manager.GetRecentLog(server.Id).ShouldHaveSingleItem().Message.ShouldBe("Map loaded");
    }

    [Fact]
    public async Task StatusTheEngineReportsReachesThoseWatching()
    {
        var extension = new FakeExtension(NewEngineId());
        var server = await AddServerAsync(extension.Id);
        var manager = await CreateManagerAsync(CreateEngines(extension));
        ServerStatusUpdateEventArgs? update = null;

        foreach (var engine in manager.Engines)
            engine.OnServerStatusUpdate += (_, args) => update = args;

        extension.Host!.ReportStatus(server.Id, ServerEngineStatus.Error, "Exited with code 1");

        update.ShouldNotBeNull();
        update.Server.Id.ShouldBe(server.Id);
        update.Status.ShouldBe(ServerProcessStatus.Error);
        update.Exception.Message.ShouldBe("Exited with code 1");
    }

    [Fact]
    public async Task ConsolesNeedTheirFeature()
    {
        var without = new FakeExtension(NewEngineId());
        var with = new FakeExtension(NewEngineId(), ServerEngineFeatures.Consoles);
        var plain = await AddServerAsync(without.Id);
        var featured = await AddServerAsync(with.Id);
        var engines = CreateEngines(without, with);
        await CreateManagerAsync(engines);

        engines.GetConsoles(plain.Id).ShouldBeEmpty();
        engines.GetConsoles(featured.Id).Select(c => c.Id).ShouldBe(["logs", "Shell"]);
        engines.GetConsoles(Guid.NewGuid()).ShouldBeEmpty();
    }

    [Fact]
    public async Task ALogConsoleIsReadFromTheEngineWithTheServersSettings()
    {
        var extension = new FakeExtension(NewEngineId(), ServerEngineFeatures.Consoles);
        var server = await AddServerAsync(extension.Id, """{"containerId":"abc"}""");
        var engines = CreateEngines(extension);
        await CreateManagerAsync(engines);

        var lines = new List<ServerEngineLogLine>();

        await foreach (var line in engines.ReadLogAsync(server.Id, "LOGS", new ServerEngineLogRequest(50), CancellationToken.None))
            lines.Add(line);

        // The engine is given its own spelling of the console's id
        lines.ShouldHaveSingleItem().Text.ShouldBe("logs of abc, last 50");
    }

    [Fact]
    public async Task ATerminalConsoleOpensAtTheGivenSizeAndCarriesInputBothWays()
    {
        var extension = new FakeExtension(NewEngineId(), ServerEngineFeatures.Consoles);
        var server = await AddServerAsync(extension.Id);
        var engines = CreateEngines(extension);
        await CreateManagerAsync(engines);

        await using var terminal = await engines.OpenTerminalAsync(server.Id, "shell", new ServerEngineTerminalSize(120, 40), CancellationToken.None);

        extension.Opened.ShouldBe([("Shell", new ServerEngineTerminalSize(120, 40))]);

        await terminal.WriteAsync("ls\r"u8.ToArray(), CancellationToken.None);

        var buffer = new byte[64];
        var read = await terminal.ReadAsync(buffer, CancellationToken.None);

        System.Text.Encoding.UTF8.GetString(buffer, 0, read).ShouldBe("ls\r");
    }

    [Fact]
    public async Task OnlyConsolesTheEngineListsCanBeOpened()
    {
        var extension = new FakeExtension(NewEngineId(), ServerEngineFeatures.Consoles);
        var server = await AddServerAsync(extension.Id);
        var local = await GetService<ServerService>().AddAsync(new ServerModel { Name = Unique("Local"), Engine = ServerEngine.Local });
        var engines = CreateEngines(extension);
        await CreateManagerAsync(engines);

        // Unknown, or of the other kind
        await Should.ThrowAsync<InvalidOperationException>(() => engines.OpenTerminalAsync(server.Id, "root", new(80, 24), CancellationToken.None));
        await Should.ThrowAsync<InvalidOperationException>(() => engines.OpenTerminalAsync(server.Id, "logs", new(80, 24), CancellationToken.None));
        Should.Throw<InvalidOperationException>(() => engines.ReadLogAsync(server.Id, "shell", new(), CancellationToken.None));

        // Not run by a plugin
        await Should.ThrowAsync<InvalidOperationException>(() => engines.OpenTerminalAsync(local.Id, "shell", new(80, 24), CancellationToken.None));

        extension.Opened.ShouldBeEmpty();
    }

    [Fact]
    public async Task AnEngineThatCannotListItsConsolesOffersNone()
    {
        var extension = new ThrowingConsoles(NewEngineId());
        var server = await AddServerAsync(extension.Id);
        var engines = CreateEngines(extension);
        await CreateManagerAsync(engines);

        engines.GetConsoles(server.Id).ShouldBeEmpty();
    }

    private sealed class ThrowingConsoles(string id) : IServerEngineExtension
    {
        public string Id => id;
        public string Name => "Throwing";
        public ServerEngineFeatures Features => ServerEngineFeatures.Consoles;

        public Task StartAsync(ServerEngineServer server, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(ServerEngineServer server, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<ServerEngineStatus> GetStatusAsync(ServerEngineServer server, CancellationToken cancellationToken) => Task.FromResult(ServerEngineStatus.Stopped);

        public IReadOnlyList<ServerEngineConsole> GetConsoles(ServerEngineServer server) => throw new InvalidOperationException("Settings unreadable");
    }

    [Fact]
    public async Task FilesNeedTheirFeature()
    {
        var without = new FakeExtension(NewEngineId());
        var plain = await AddServerAsync(without.Id);
        var manager = await CreateManagerAsync(CreateEngines(without));

        manager.SupportsFiles(plain.Id).ShouldBeFalse();
        await Should.ThrowAsync<NotSupportedException>(() => manager.ListFilesAsync(plain.Id, ""));

        without.FilePaths.ShouldBeEmpty();
    }

    [Fact]
    public async Task FilesAreReadAndWrittenThroughTheEngine()
    {
        var extension = new FakeExtension(NewEngineId(), ServerEngineFeatures.Files);
        var server = await AddServerAsync(extension.Id);
        var manager = await CreateManagerAsync(CreateEngines(extension));

        manager.SupportsFiles(server.Id).ShouldBeTrue();

        await manager.WriteFileAsync(server.Id, @"\cfg\server.cfg", new MemoryStream("hostname LAN"u8.ToArray()));

        var listed = (await manager.ListFilesAsync(server.Id, "cfg/")).ShouldHaveSingleItem();
        listed.Path.ShouldBe("cfg/server.cfg");
        listed.Size.ShouldBe(12);

        await using (var stream = await manager.OpenFileAsync(server.Id, "cfg/server.cfg"))
            (await new StreamReader(stream).ReadToEndAsync()).ShouldBe("hostname LAN");

        await manager.DeleteFileAsync(server.Id, "cfg/server.cfg");
        extension.Files.ShouldBeEmpty();

        // The engine sees each path in its one spelling
        extension.FilePaths.ShouldBe(["cfg/server.cfg", "cfg", "cfg/server.cfg", "cfg/server.cfg"]);
    }

    [Fact]
    public async Task FilePathsCantLeaveTheServersFiles()
    {
        var extension = new FakeExtension(NewEngineId(), ServerEngineFeatures.Files);
        var server = await AddServerAsync(extension.Id);
        var manager = await CreateManagerAsync(CreateEngines(extension));

        await Should.ThrowAsync<ArgumentException>(() => manager.ListFilesAsync(server.Id, "../.."));
        await Should.ThrowAsync<ArgumentException>(() => manager.OpenFileAsync(server.Id, "cfg/../../secrets"));
        await Should.ThrowAsync<ArgumentException>(() => manager.DeleteFileAsync(server.Id, "/"));

        extension.FilePaths.ShouldBeEmpty();
    }

    [Fact]
    public void AnEngineWhoseIdIsTakenIsLeftOut()
    {
        var id = NewEngineId();
        var first = new FakeExtension(id);

        var engines = CreateEngines(first, new FakeExtension(id.ToUpperInvariant()), new FakeExtension(""));

        engines.Extensions.ShouldBe([first]);
        engines.Find(id.ToUpperInvariant()).ShouldBe(first);
    }

    private sealed class Settings
    {
        public string ContainerId { get; set; } = "";
    }
}
