using System.Collections.Concurrent;
using System.Runtime.Versioning;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.PowerShell;
using LANCommander.SDK.PowerShell.Debugging;
using LANCommander.SDK.PowerShell.Debugging.Hosting;
using LANCommander.SDK.PowerShell.Debugging.Remote;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using GameManifest = LANCommander.SDK.Models.Manifest.Game;
using SdkSettings = LANCommander.SDK.Models.Settings;

namespace LANCommander.SDK.Tests.PowerShell.Debugging;

/// <summary>
/// Cross-process debugging, exercised in one process: the "launcher" hosts a real
/// <see cref="ScriptDebugPipeServer"/>, and a <see cref="PowerShellScript"/> configured with a
/// <see cref="PipeScriptDebugBroker"/> plays the elevated child, talking to it over an actual named pipe.
/// </summary>
[Collection("PowerShellDebugger")]
public sealed class RemoteDebuggingTests : IDisposable
{
    private readonly string _installDirectory;
    private readonly Guid _gameId = Guid.NewGuid();

    public RemoteDebuggingTests()
    {
        _installDirectory = Path.Combine(Path.GetTempPath(), $"lc-psdbg-remote-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_installDirectory);
    }

    public void Dispose()
    {
        try { Directory.Delete(_installDirectory, recursive: true); }
        catch (Exception) { }
    }

    [Fact]
    public async Task Messages_RoundTripThroughTheChannel()
    {
        using var stream = new MemoryStream();
        using var channel = new DebugMessageChannel(stream, leaveOpen: true);

        DebugMessage[] messages =
        [
            new HelloMessage { ProtocolVersion = 1, Token = "t", ProcessId = 5, IsElevated = true },
            new AttachRequest
            {
                RequestId = 3,
                Identity = new ScriptIdentity(new ScriptKey(_gameId, ScriptType.Install), ScriptOwnerKind.Game, _gameId, @"C:\Games", @"C:\Games\x.ps1"),
            },
            new AttachResponse { RequestId = 3, Accepted = true, Breakpoints = [new BreakpointRequest(@"C:\x.ps1", 4, true)] },
            new OutputMessage { Kind = ConsoleOutputKind.Warning, Text = "careful", NewLine = true, Foreground = ConsoleColor.Yellow },
            new StoppedEvent
            {
                Info = new DebuggerStopInfo
                {
                    ScriptName = @"C:\x.ps1",
                    LineNumber = 4,
                    ColumnNumber = 1,
                    HitBreakpointIds = [1],
                    Frames = [new CallStackFrameInfo { Index = 0, FunctionName = "<ScriptBlock>", ScriptName = @"C:\x.ps1", LineNumber = 4 }],
                },
            },
            new EvaluateResponse { RequestId = 9, Result = new EvaluationResult("42", false, null) },
            new VariablesResponse { Variables = [new VariableInfo { Name = "a", TypeName = "Int32", Value = "1", HasChildren = false }] },
            new RunCompletedEvent { ExitCode = 3, Faulted = false, DurationMilliseconds = 10 },
        ];

        foreach (var message in messages)
            await channel.WriteAsync(message);

        stream.Position = 0;

        foreach (var expected in messages)
        {
            var actual = await channel.ReadAsync();

            Assert.NotNull(actual);
            Assert.IsType(expected.GetType(), actual);
        }

        stream.Position = 0;

        await channel.ReadAsync();
        var attach = Assert.IsType<AttachRequest>(await channel.ReadAsync());
        Assert.Equal(new ScriptKey(_gameId, ScriptType.Install), attach.Identity.Key);

        var attachResponse = Assert.IsType<AttachResponse>(await channel.ReadAsync());
        Assert.Equal(4, Assert.Single(attachResponse.Breakpoints).Line);

        var output = Assert.IsType<OutputMessage>(await channel.ReadAsync());
        Assert.Equal(ConsoleOutputKind.Warning, output.Kind);
        Assert.Equal(ConsoleColor.Yellow, output.Foreground);

        var stopped = Assert.IsType<StoppedEvent>(await channel.ReadAsync());
        Assert.Equal(4, stopped.Info.LineNumber);
        Assert.Equal("<ScriptBlock>", Assert.Single(stopped.Info.Frames).FunctionName);

        var evaluation = Assert.IsType<EvaluateResponse>(await channel.ReadAsync());
        Assert.Equal("42", evaluation.Result.Output);
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task ElevatedScript_StopsAtBreakpoints_AndIsInspectableThroughThePipe()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var target = new FakeTarget(_gameId) { BreakpointLines = [2] };
        target.Sink.InputAnswers.Enqueue("typed in the launcher");

        await using var server = new ScriptDebugPipeServer(target);
        server.Start();

        await using var broker = new PipeScriptDebugBroker(server.Endpoint.PipeName, server.Endpoint.Token);

        var script = GameScript(broker, """
            Write-Host 'hello from the child'
            $answer = Read-Host 'Say something'
            $Return = "$answer / $InstallDirectory"
            """);

        Assert.True(script.IsDebuggerAttached);

        var run = script.ExecuteAsync<string>();

        var session = await target.WaitForSessionAsync();
        Assert.True(session.IsRemote);
        Assert.Equal(Environment.ProcessId, session.ProcessId);

        var stop = target.WaitForStop();
        Assert.NotNull(stop);
        Assert.Equal(2, stop.LineNumber);

        var evaluated = await session.EvaluateAsync("$InstallDirectory", TimeSpan.FromSeconds(10));
        Assert.False(evaluated.IsError, evaluated.Output);
        Assert.Equal(_installDirectory, evaluated.Output.Trim());

        var variables = await session.GetFrameVariablesAsync(0, TimeSpan.FromSeconds(10));
        Assert.Contains(variables, v => v.Name == "InstallDirectory");

        session.Resume(DebugResumeKind.Continue);

        Assert.Equal($"typed in the launcher / {_installDirectory}", await run.WaitAsync(TimeSpan.FromSeconds(30)));

        var completion = await target.WaitForCompletionAsync();
        Assert.False(completion.Faulted, completion.ErrorMessage);

        Assert.Contains(target.Sink.TextOf(ConsoleOutputKind.Host), t => t.Contains("hello from the child"));
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task Stop_FromTheLauncher_EndsTheElevatedRun()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var target = new FakeTarget(_gameId) { BreakpointLines = [1] };

        await using var server = new ScriptDebugPipeServer(target);
        server.Start();

        await using var broker = new PipeScriptDebugBroker(server.Endpoint.PipeName, server.Endpoint.Token);

        var script = GameScript(broker, """
            $Return = 1
            """);

        var run = script.ExecuteAsync<int>();

        var session = await target.WaitForSessionAsync();
        Assert.NotNull(target.WaitForStop());

        session.RequestStop();

        Assert.Equal(0, await run.WaitAsync(TimeSpan.FromSeconds(30)));
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task Detach_FromTheLauncher_LetsTheElevatedRunFinish()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var target = new FakeTarget(_gameId) { BreakpointLines = [1] };

        await using var server = new ScriptDebugPipeServer(target);
        server.Start();

        await using var broker = new PipeScriptDebugBroker(server.Endpoint.PipeName, server.Endpoint.Token);

        var script = GameScript(broker, """
            $Return = 6
            """);

        var run = script.ExecuteAsync<int>();

        var session = await target.WaitForSessionAsync();
        Assert.NotNull(target.WaitForStop());

        session.Detach();

        Assert.Equal(6, await run.WaitAsync(TimeSpan.FromSeconds(30)));
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task LosingThePipe_WhileStopped_LetsTheElevatedRunFinish()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var target = new FakeTarget(_gameId) { BreakpointLines = [1] };

        var server = new ScriptDebugPipeServer(target);
        server.Start();

        await using var broker = new PipeScriptDebugBroker(server.Endpoint.PipeName, server.Endpoint.Token);

        var script = GameScript(broker, """
            $Return = 8
            """);

        var run = script.ExecuteAsync<int>();

        await target.WaitForSessionAsync();
        Assert.NotNull(target.WaitForStop());

        // The launcher closes (or crashes) with the script parked at a breakpoint.
        await server.DisposeAsync();

        Assert.Equal(8, await run.WaitAsync(TimeSpan.FromSeconds(30)));

        var completion = await target.WaitForCompletionAsync();
        Assert.True(completion.Faulted);
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task WrongToken_IsRejected_AndTheScriptRunsWithoutTheDebugger()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var target = new FakeTarget(_gameId) { BreakpointLines = [1] };

        await using var server = new ScriptDebugPipeServer(target);
        server.Start();

        await using var broker = new PipeScriptDebugBroker(server.Endpoint.PipeName, "not-the-token");

        var script = GameScript(broker, "$Return = 2");

        Assert.False(script.IsDebuggerAttached);
        Assert.Equal(2, await script.ExecuteAsync<int>());
        Assert.Equal(0, target.AttachCount);
    }

    private PowerShellScript GameScript(IScriptDebugBroker broker, string contents)
    {
        var path = ScriptHelper.GetScriptFilePath(_installDirectory, _gameId, ScriptType.Install);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<ISettingsProvider, FakeSettingsProvider>();
        services.AddSingleton(broker);

        return new PowerShellScript(services.BuildServiceProvider(), ScriptType.Install, Options.Create(new SdkSettings()))
            .AddVariable("InstallDirectory", _installDirectory)
            .AddVariable("GameManifest", new GameManifest { Id = _gameId })
            .UseWorkingDirectory(_installDirectory)
            .UseFile(path);
    }

    private sealed class FakeTarget(Guid gameId) : IScriptDebugTarget
    {
        private readonly TaskCompletionSource<IDebugSessionHandle> _session = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<RunCompletion> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly BlockingCollection<DebuggerStopInfo> _stops = new();

        public TestConsoleSink Sink { get; } = new();

        public int[] BreakpointLines { get; init; } = [];

        public int AttachCount { get; private set; }

        public ScriptDebugRemoteEndpoint? RemoteEndpoint => null;

        public bool Matches(ScriptIdentity identity) => identity.GameId == gameId;

        public void PrepareScriptFile(ScriptIdentity identity, string path) { }

        public ScriptDebugAttachment? BeginAttach(ScriptIdentity identity)
        {
            AttachCount++;

            return new ScriptDebugAttachment
            {
                Sink = Sink,
                Breakpoints = BreakpointLines.Select(l => new BreakpointRequest(identity.ScriptPath!, l, true)).ToArray(),
                SessionStarted = session =>
                {
                    session.Stopped += info => _stops.Add(info);
                    session.RunCompleted += completion => _completion.TrySetResult(completion);
                    _session.TrySetResult(session);
                },
            };
        }

        public Task<IDebugSessionHandle> WaitForSessionAsync() => _session.Task.WaitAsync(TimeSpan.FromSeconds(30));

        public Task<RunCompletion> WaitForCompletionAsync() => _completion.Task.WaitAsync(TimeSpan.FromSeconds(30));

        public DebuggerStopInfo? WaitForStop() =>
            _stops.TryTake(out var info, TimeSpan.FromSeconds(30)) ? info : null;
    }

    private sealed class FakeSettingsProvider : ISettingsProvider
    {
        public SdkSettings CurrentValue { get; } = new();

        public void Update(Action<SdkSettings> patch) => patch(CurrentValue);
    }
}
