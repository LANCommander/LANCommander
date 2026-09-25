using System.Collections.Concurrent;
using System.Management.Automation.Runspaces;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Enums;
using LANCommander.SDK.PowerShell;
using LANCommander.SDK.PowerShell.Debugging;
using LANCommander.SDK.PowerShell.Debugging.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SdkSettings = LANCommander.SDK.Models.Settings;

namespace LANCommander.SDK.Tests.PowerShell.Debugging;

/// <summary>
/// Drives a <see cref="DebugSession"/> the way the UI does: it never blocks the session's threads, and
/// it only talks to the debugger through the session's public API.
/// </summary>
internal sealed class DebugSessionHarness : IDisposable
{
    private readonly BlockingCollection<DebuggerStopInfo> _stops = new();
    private readonly ManualResetEventSlim _completed = new(false);
    private readonly ConcurrentDictionary<DebugSessionState, ManualResetEventSlim> _stateGates = new();
    private readonly string _path;

    public DebugSessionHarness(string path)
    {
        _path = path;
        Sink = new TestConsoleSink();
        Session = new DebugSession(Sink);

        Session.Stopped += info => _stops.Add(info);
        Session.RunCompleted += completion =>
        {
            LastCompletion = completion;
            _completed.Set();
        };
        Session.StateChanged += state => Gate(state).Set();
    }

    public TestConsoleSink Sink { get; }

    public DebugSession Session { get; }

    public RunCompletion? LastCompletion { get; private set; }

    public static PowerShellRunspaceBuilder CreateBuilder()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<ISettingsProvider, FakeSettingsProvider>();

        return new PowerShellRunspaceBuilder(services.BuildServiceProvider(), NullLogger.Instance);
    }

    public static CommandCatalog CreateCatalog() => new(() => CreateBuilder().Open());

    private ManualResetEventSlim Gate(DebugSessionState state) =>
        _stateGates.GetOrAdd(state, _ => new ManualResetEventSlim(false));

    public void Start(
        IReadOnlyList<BreakpointRequest> breakpoints,
        bool stepIntoOnStart = false,
        string? contents = null,
        Func<System.Management.Automation.PowerShell, IReadOnlyList<object?>, object?>? captureResult = null,
        IEnumerable<PowerShellVariable>? variables = null)
    {
        _completed.Reset();

        var builder = CreateBuilder();
        var workingDirectory = Path.GetDirectoryName(_path)!;

        Session.Start(new DebugLaunchRequest
        {
            ScriptPath = _path,
            ScriptContents = contents ?? File.ReadAllText(_path),
            Breakpoints = breakpoints,
            StepIntoOnStart = stepIntoOnStart,
            CaptureResult = captureResult,
            OpenRunspace = host =>
            {
                var runspace = builder.Open(host, PSThreadOptions.UseCurrentThread);

                builder.Initialize(runspace, workingDirectory, ScriptType.Install, variables ?? [], "logo");

                return runspace;
            },
        });
    }

    public void Reset()
    {
        while (_stops.TryTake(out _)) { }

        _completed.Reset();

        foreach (var gate in _stateGates.Values)
            gate.Reset();
    }

    public DebuggerStopInfo? WaitForStop(TimeSpan? timeout = null) =>
        _stops.TryTake(out var info, (int)(timeout ?? TimeSpan.FromSeconds(30)).TotalMilliseconds) ? info : null;

    public bool WaitForCompletion(TimeSpan? timeout = null) =>
        _completed.Wait(timeout ?? TimeSpan.FromSeconds(30));

    public bool WaitForState(DebugSessionState state, TimeSpan timeout) =>
        Session.State == state || Gate(state).Wait(timeout);

    /// <summary>A script may stop several more times (further breakpoint hits) before it ends.</summary>
    public void DrainStops()
    {
        while (!_completed.IsSet)
        {
            var info = WaitForStop(TimeSpan.FromSeconds(2));

            if (info is null)
                return;

            Session.Resume(DebugResumeKind.Continue);
        }
    }

    public EvaluationResult Evaluate(string expression) =>
        Session.EvaluateAsync(expression, TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();

    public IReadOnlyList<VariableInfo> FrameVariables(int scope) =>
        Session.GetFrameVariablesAsync(scope, TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();

    public IReadOnlyList<VariableInfo> Expand(int handle) =>
        Session.ExpandVariableAsync(handle, TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();

    public IReadOnlyList<CommandInfoSnapshot> Commands() =>
        Session.GetCommandsAsync(TimeSpan.FromSeconds(60)).GetAwaiter().GetResult();

    public CommandDetail? CommandDetail(string name) =>
        Session.GetCommandDetailAsync(name, TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();

    public void Dispose()
    {
        Session.Dispose();
        _completed.Dispose();
        _stops.Dispose();
    }

    private sealed class FakeSettingsProvider : ISettingsProvider
    {
        public SdkSettings CurrentValue { get; } = new();

        public void Update(Action<SdkSettings> patch) => patch(CurrentValue);
    }
}
