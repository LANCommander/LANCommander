using System.Collections.Concurrent;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.PowerShell;
using LANCommander.SDK.PowerShell.Debugging;
using LANCommander.SDK.PowerShell.Debugging.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using GameManifest = LANCommander.SDK.Models.Manifest.Game;
using RedistributableManifest = LANCommander.SDK.Models.Manifest.Redistributable;
using SdkSettings = LANCommander.SDK.Models.Settings;

namespace LANCommander.SDK.Tests.PowerShell.Debugging;

/// <summary>
/// Drives <see cref="PowerShellScript"/> through the broker the way the launcher does, checking that a
/// debugged run behaves exactly like a normal one from the script's point of view.
/// </summary>
[Collection("PowerShellDebugger")]
public sealed class PowerShellScriptDebugTests : IDisposable
{
    private readonly string _installDirectory;
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly ScriptDebugBroker _broker = new();

    public PowerShellScriptDebugTests()
    {
        _installDirectory = Path.Combine(Path.GetTempPath(), $"lc-psdbg-install-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_installDirectory);
    }

    public void Dispose()
    {
        try { Directory.Delete(_installDirectory, recursive: true); }
        catch (Exception) { }
    }

    private PowerShellScript CreateScript(ScriptType type = ScriptType.Install, bool withBroker = true)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<ISettingsProvider, FakeSettingsProvider>();

        if (withBroker)
            services.AddSingleton<IScriptDebugBroker>(_broker);

        return new PowerShellScript(services.BuildServiceProvider(), type, Options.Create(new SdkSettings()));
    }

    private string WriteScript(Guid ownerId, ScriptType type, string contents)
    {
        var path = ScriptHelper.GetScriptFilePath(_installDirectory, ownerId, type);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);

        return path;
    }

    private PowerShellScript GameScript(string contents, ScriptType type = ScriptType.Install, bool withBroker = true)
    {
        var path = WriteScript(_gameId, type, contents);

        return CreateScript(type, withBroker)
            .AddVariable("InstallDirectory", _installDirectory)
            .AddVariable("GameManifest", new GameManifest { Id = _gameId, Title = "Test Game" })
            .UseWorkingDirectory(_installDirectory)
            .UseFile(path);
    }

    [Fact]
    public void Identity_IsDerivedFromThePathAndManifestVariables()
    {
        var script = GameScript("$Return = 1", ScriptType.BeforeStart);

        var identity = script.Identity;

        Assert.NotNull(identity);
        Assert.Equal(new ScriptKey(_gameId, ScriptType.BeforeStart), identity.Key);
        Assert.Equal(ScriptOwnerKind.Game, identity.OwnerKind);
        Assert.Equal(_gameId, identity.GameId);
        Assert.Equal(Path.GetFullPath(_installDirectory), identity.InstallDirectory);
    }

    [Fact]
    public void Identity_RecognisesRedistributableScripts()
    {
        var redistributableId = Guid.NewGuid();
        var path = WriteScript(redistributableId, ScriptType.DetectInstall, "$Return = $true");

        var script = CreateScript(ScriptType.DetectInstall)
            .AddVariable("GameManifest", new GameManifest { Id = _gameId })
            .AddVariable("RedistributableManifest", new RedistributableManifest { Id = redistributableId })
            .UseFile(path);

        Assert.Equal(ScriptOwnerKind.Redistributable, script.Identity!.OwnerKind);
        Assert.Equal(redistributableId, script.Identity.Key.OwnerId);
        Assert.Equal(_gameId, script.Identity.GameId);
    }

    [Fact]
    public void InlineScripts_HaveNoIdentity()
    {
        Assert.Null(CreateScript().UseInline("$Return = 1").Identity);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Result_IsTheSame_WithAndWithoutTheDebugger(bool attach)
    {
        using var target = attach ? new FakeTarget(_gameId) : null;
        using var registration = target is null ? null : _broker.Register(target);

        var script = GameScript("""
            $value = $InstallDirectory.Length
            $Return = 40 + 2
            """);

        Assert.Equal(attach, script.IsDebuggerAttached);
        Assert.Equal(42, await script.ExecuteAsync<int>());

        if (target is not null)
            Assert.Equal(1, target.AttachCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LastPipelineOutput_IsTheFallbackResult_WithAndWithoutTheDebugger(bool attach)
    {
        using var target = attach ? new FakeTarget(_gameId) : null;
        using var registration = target is null ? null : _broker.Register(target);

        var script = GameScript("""
            'ignored'
            'the answer'
            """);

        Assert.Equal("the answer", await script.ExecuteAsync<string>());
    }

    [Fact]
    public async Task Breakpoints_FromTheAttachment_StopTheScript_AndVariablesAreVisible()
    {
        using var target = new FakeTarget(_gameId) { BreakpointLines = [2] };
        using var registration = _broker.Register(target);

        var script = GameScript("""
            $first = 'one'
            $second = "$InstallDirectory"
            $Return = 7
            """);

        var run = script.ExecuteAsync<int>();

        var session = await target.WaitForSessionAsync();
        var stop = target.WaitForStop();

        Assert.NotNull(stop);
        Assert.Equal(2, stop.LineNumber);

        var install = (await session.EvaluateAsync("$InstallDirectory", TimeSpan.FromSeconds(10))).Output.Trim();
        Assert.Equal(_installDirectory, install);

        session.Resume(DebugResumeKind.Continue);

        Assert.Equal(7, await run.WaitAsync(TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public async Task Stop_WhileStoppedAtABreakpoint_EndsTheRun()
    {
        using var target = new FakeTarget(_gameId) { BreakpointLines = [1] };
        using var registration = _broker.Register(target);

        var script = GameScript("""
            $Return = 1
            $Return = 2
            """);

        var run = script.ExecuteAsync<int>();

        await target.WaitForSessionAsync();
        Assert.NotNull(target.WaitForStop());

        script.Stop();

        // Stopped before line 1 ran, so there is no result.
        Assert.Equal(0, await run.WaitAsync(TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public async Task Detach_WhileStopped_LetsTheRunComplete()
    {
        using var target = new FakeTarget(_gameId) { BreakpointLines = [1] };
        using var registration = _broker.Register(target);

        var script = GameScript("""
            $Return = 5
            """);

        var run = script.ExecuteAsync<int>();

        var session = (DebugSession)await target.WaitForSessionAsync();
        Assert.NotNull(target.WaitForStop());

        session.Detach();

        Assert.Equal(5, await run.WaitAsync(TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public async Task WriteHost_ReachesTheDebuggerConsole_Once()
    {
        using var target = new FakeTarget(_gameId);
        using var registration = _broker.Register(target);

        var script = GameScript("Write-Host 'hello from the script'");

        await script.ExecuteAsync<int>();

        Assert.Single(target.Sink.TextOf(ConsoleOutputKind.Host), t => t.Contains("hello from the script"));
    }

    [Fact]
    public async Task ScriptsForOtherGames_RunWithoutTheDebugger()
    {
        using var target = new FakeTarget(Guid.NewGuid());
        using var registration = _broker.Register(target);

        var script = GameScript("$Return = 3");

        Assert.False(script.IsDebuggerAttached);
        Assert.Equal(3, await script.ExecuteAsync<int>());
        Assert.Equal(0, target.AttachCount);
        Assert.Empty(target.Sink.Lines);
    }

    [Fact]
    public async Task WithoutABroker_ScriptsRunNormally()
    {
        var script = GameScript("$Return = 9", withBroker: false);

        Assert.False(script.IsDebuggerAttached);
        Assert.Equal(9, await script.ExecuteAsync<int>());
    }

    [Fact]
    public async Task PrepareScriptFile_RunsBeforeTheFileIsRead()
    {
        using var target = new FakeTarget(_gameId) { ReplacementContents = "$Return = 99" };
        using var registration = _broker.Register(target);

        var script = GameScript("$Return = 1");

        Assert.Equal(99, await script.ExecuteAsync<int>());
    }

    [Fact]
    public async Task BusyTarget_RunsTheScriptWithoutTheDebugger()
    {
        using var target = new FakeTarget(_gameId) { Busy = true };
        using var registration = _broker.Register(target);

        var script = GameScript("$Return = 4");

        Assert.Equal(4, await script.ExecuteAsync<int>());
        Assert.Empty(target.Sink.Lines);
    }

    private sealed class FakeTarget(Guid gameId) : IScriptDebugTarget, IDisposable
    {
        private readonly TaskCompletionSource<IDebugSessionHandle> _session = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly BlockingCollection<DebuggerStopInfo> _stops = new();

        public TestConsoleSink Sink { get; } = new();

        public int[] BreakpointLines { get; init; } = [];

        public string? ReplacementContents { get; init; }

        public bool Busy { get; init; }

        public int AttachCount { get; private set; }

        public ScriptDebugRemoteEndpoint? RemoteEndpoint => null;

        public bool Matches(ScriptIdentity identity) => identity.GameId == gameId;

        public void PrepareScriptFile(ScriptIdentity identity, string path)
        {
            if (ReplacementContents is not null)
                File.WriteAllText(path, ReplacementContents);
        }

        public ScriptDebugAttachment? BeginAttach(ScriptIdentity identity)
        {
            if (Busy)
                return null;

            AttachCount++;

            return new ScriptDebugAttachment
            {
                Sink = Sink,
                Breakpoints = BreakpointLines.Select(l => new BreakpointRequest(identity.ScriptPath!, l, true)).ToArray(),
                SessionStarted = session =>
                {
                    session.Stopped += info => _stops.Add(info);
                    _session.TrySetResult(session);
                },
            };
        }

        public Task<IDebugSessionHandle> WaitForSessionAsync() => _session.Task.WaitAsync(TimeSpan.FromSeconds(30));

        public DebuggerStopInfo? WaitForStop() =>
            _stops.TryTake(out var info, TimeSpan.FromSeconds(30)) ? info : null;

        public void Dispose() => _stops.Dispose();
    }

    private sealed class FakeSettingsProvider : ISettingsProvider
    {
        public SdkSettings CurrentValue { get; } = new();

        public void Update(Action<SdkSettings> patch) => patch(CurrentValue);
    }
}
