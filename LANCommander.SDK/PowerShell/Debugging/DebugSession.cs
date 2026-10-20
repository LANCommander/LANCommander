#nullable enable
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK.PowerShell.Debugging.Commands;
using LANCommander.SDK.PowerShell.Debugging.Hosting;
using LANCommander.SDK.PowerShell.Debugging.Scripting;
using PowerShellInstance = System.Management.Automation.PowerShell;
using SmaDebugger = System.Management.Automation.Debugger;

namespace LANCommander.SDK.PowerShell.Debugging;

/// <summary>
/// Owns one debugging run: a runspace, a dedicated pipeline thread, and the work-item pump that services
/// the UI while the debugger is stopped.
/// </summary>
/// <remarks>
/// <para><b>Threading contract. Two rules, both load-bearing.</b></para>
/// <para>
/// 1. The DebuggerStop handler must never wait on the UI thread. At the moment it fires, the pump is not
/// yet consuming; if a UI delegate needed the debugger it would enqueue work that only this thread can
/// service, and this thread would be waiting for that delegate. That deadlock is circular, untimed and
/// unrecoverable. The session therefore raises plain events and never references a dispatcher; the UI
/// marshals with Post.
/// </para>
/// <para>
/// 2. The UI thread must never block on a work item (no .Wait(), no .Result). Between enqueue and
/// service, the script can resume or hit exit; awaiting turns that into a faulted task and a visible
/// error, while blocking turns it into a frozen application.
/// </para>
/// <para>Consequence: the pipeline thread is the only thread that ever touches the engine's debugger.</para>
/// </remarks>
public sealed class DebugSession : IDebugSessionHandle, IDisposable
{
    /// <summary>Global variable holding the compiled script while it runs.</summary>
    public const string CompiledScriptVariable = "__LCDebugScript";

    /// <summary>Global variable the script's raw pipeline output is teed into.</summary>
    public const string OutputCaptureVariable = "__LCOutput";

    private static int _executionPolicyReported;

    private readonly SwitchableConsoleSink _sink;
    private readonly DebugPSHost _host;
    private readonly ManualResetEventSlim _pipelineCompleted = new(initialState: true);
    private readonly ConcurrentQueue<PendingBreakpointChange> _pendingChanges = new();

    // Pipeline-thread only.
    private readonly Dictionary<int, int> _breakpointIdToLine = new();
    private readonly Dictionary<int, Breakpoint> _lineToBreakpoint = new();

    private BlockingCollection<DebuggerWorkItem>? _pump;
    private int _nestingDepth;
    private int _pumpResumeRequest;
    private int _stopEscalation;
    private int _detached;
    private bool _skipUntilScript;

    private Runspace? _runspace;
    private PowerShellInstance? _powerShell;
    private SmaDebugger? _debugger;
    private CancellationTokenSource? _cancellation;
    private Thread? _pipelineThread;
    private TaskCompletionSource<RunCompletion>? _completion;
    private int _state = (int)DebugSessionState.Idle;

    public DebugSession(IConsoleSink sink)
    {
        _sink = new SwitchableConsoleSink(sink);
        _host = new DebugPSHost(_sink);
    }

    /// <summary>Raised on the pipeline thread when the debugger stops. The UI marshals it.</summary>
    public event Action<DebuggerStopInfo>? Stopped;

    /// <summary>Raised on the pipeline thread when the debugger leaves a stop.</summary>
    public event Action? Resumed;

    public event Action<DebugSessionState>? StateChanged;

    public event Action<BreakpointUpdate>? BreakpointChanged;

    public event Action<RunCompletion>? RunCompleted;

    public DebugSessionState State => (DebugSessionState)Volatile.Read(ref _state);

    /// <summary>The normalised path of the script this run is executing.</summary>
    public string ScriptPath { get; private set; } = string.Empty;

    public bool IsRemote => false;

    public int? ProcessId => null;

    /// <summary>True once <see cref="Detach"/> has been called.</summary>
    public bool IsDetached => Volatile.Read(ref _detached) != 0;

    /// <summary>Completes when the current (or last) run finishes. Never faults.</summary>
    public Task<RunCompletion> Completion =>
        _completion?.Task ?? Task.FromResult(new RunCompletion(null, false, null, TimeSpan.Zero));

    internal VariableExpander Expander { get; } = new();

    public bool IsBusy => State is not DebugSessionState.Idle;

    // ---- lifecycle ----------------------------------------------------------------------

    /// <summary>
    /// Begin a run. Returns immediately; progress arrives through the events above and
    /// <see cref="Completion"/>.
    /// </summary>
    public void Start(DebugLaunchRequest request)
    {
        if (IsBusy)
            throw new InvalidOperationException("A debug session is already running.");

        ScriptPath = Scripting.ScriptPath.Normalize(request.ScriptPath);
        _pendingChanges.Clear();
        Interlocked.Exchange(ref _stopEscalation, 0);
        Interlocked.Exchange(ref _pumpResumeRequest, 0);

        _cancellation = new CancellationTokenSource();
        _host.ResetPerRunState();
        _host.SetInputCancellation(_cancellation.Token);

        _completion = new TaskCompletionSource<RunCompletion>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pipelineCompleted.Reset();
        SetState(DebugSessionState.Starting);

        _pipelineThread = new Thread(() => PipelineThreadBody(request))
        {
            IsBackground = true,
            Name = "PS-Pipeline",
        };

        // STA matches pwsh.exe on Windows and keeps COM-using scripts working. Combined with
        // PSThreadOptions.UseCurrentThread, all engine work happens on this one thread.
        if (OperatingSystem.IsWindows())
            _pipelineThread.SetApartmentState(ApartmentState.STA);

        _pipelineThread.Start();
    }

    private void PipelineThreadBody(DebugLaunchRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        var faulted = false;
        string? errorMessage = null;
        object? result = null;
        var output = new PSDataCollection<PSObject>();

        try
        {
            request.OnPipelineThreadStarted?.Invoke();

            _runspace = request.OpenRunspace(_host);

            // The banner runs before the debugger is armed so stepping never lands in it.
            if (!string.IsNullOrWhiteSpace(request.Preamble))
                RunPreamble(request.Preamble!);

            var debugger = _runspace.Debugger
                ?? throw new InvalidOperationException("The runspace exposed no debugger.");
            _debugger = debugger;

            // Must be after Open (the debugger is not usable on a closed runspace) and before Invoke. A
            // hosted runspace defaults to DebugModes.None, which is why "my breakpoints never fire" is the
            // classic first-run symptom.
            debugger.SetDebugMode(DebugModes.LocalScript);
            debugger.DebuggerStop += OnDebuggerStop;
            debugger.BreakpointUpdated += OnBreakpointUpdated;

            if (!IsDetached)
            {
                ApplyBreakpoints(debugger, request.Breakpoints);

                if (request.StepIntoOnStart)
                {
                    _skipUntilScript = true;
                    debugger.SetDebuggerStepMode(true);
                }
            }

            ReportExecutionPolicyOnce();

            // Compile the text with the script's path as its source. AddScript(text) would produce a
            // ScriptBlock whose Extent.File is null, which no line breakpoint can ever match. Parsing
            // with a path gives every statement an extent in that file, so breakpoints set against the
            // path bind, while the text that runs is exactly what the caller handed us.
            var ast = Parser.ParseInput(request.ScriptContents, ScriptPath, out _, out var parseErrors);

            if (parseErrors is { Length: > 0 })
            {
                foreach (var error in parseErrors)
                    _sink.WriteLine(ConsoleOutputKind.Error,
                        $"{error.Message} ({System.IO.Path.GetFileName(ScriptPath)}:{error.Extent.StartLineNumber})");

                throw new ParseException(parseErrors);
            }

            _runspace.SessionStateProxy.SetVariable(CompiledScriptVariable, ast.GetScriptBlock());

            _powerShell = PowerShellInstance.Create();
            _powerShell.Runspace = _runspace;

            HookStreams(_powerShell, output);

            // Dot-sourced so the script runs in the global scope exactly as an AddScript(text) call
            // would: $Return lands where the caller reads it back. Tee-Object keeps the raw objects
            // for callers that fall back to the last pipeline output; Out-String renders them for the
            // console so Format-Table arrives as text rather than as bare PSObjects.
            _powerShell
                .AddScript($". ${CompiledScriptVariable} | Tee-Object -Variable {OutputCaptureVariable}")
                .AddCommand("Out-String")
                .AddParameter("Stream", true)
                .AddParameter("Width", 200);

            SetState(DebugSessionState.Running);
            _powerShell.Invoke(input: null, output: output);
        }
        catch (PipelineStoppedException)
        {
            // Stop was requested, or a blocked Read-Host was cancelled. Not an error.
        }
        catch (Exception ex)
        {
            faulted = true;
            errorMessage = ex.GetBaseException().Message;

            if (ex is not ParseException)
                _sink.WriteLine(ConsoleOutputKind.Error, errorMessage);
        }
        finally
        {
            stopwatch.Stop();
            var exitCode = _host.LastExitCode;

            result = CaptureResult(request);

            CleanupEngine();

            try { request.OnPipelineThreadCompleted?.Invoke(); }
            catch (Exception) { }

            SetState(DebugSessionState.Idle);
            _pipelineCompleted.Set();

            var completion = new RunCompletion(exitCode, faulted, errorMessage, stopwatch.Elapsed) { Result = result };

            try { RunCompleted?.Invoke(completion); }
            catch (Exception) { }

            _completion?.TrySetResult(completion);
        }
    }

    private void RunPreamble(string preamble)
    {
        try
        {
            using var powerShell = PowerShellInstance.Create();
            powerShell.Runspace = _runspace;
            powerShell.AddScript(preamble);
            powerShell.Invoke();
        }
        catch (Exception)
        {
            // Cosmetic only.
        }
    }

    private object? CaptureResult(DebugLaunchRequest request)
    {
        if (request.CaptureResult is null || _powerShell is null || _runspace is null)
            return null;

        try
        {
            var raw = new List<object?>();
            var captured = _runspace.SessionStateProxy.GetVariable(OutputCaptureVariable);

            switch (captured)
            {
                case null:
                    break;
                case IEnumerable enumerable and not string:
                    foreach (var item in enumerable)
                        raw.Add(item);
                    break;
                default:
                    raw.Add(captured);
                    break;
            }

            return request.CaptureResult(_powerShell, raw);
        }
        catch (Exception ex)
        {
            _sink.WriteLine(ConsoleOutputKind.System, "[debugger] Could not read the script's result: " + ex.GetBaseException().Message);
            return null;
        }
    }

    // ---- the pump -----------------------------------------------------------------------

    private void OnDebuggerStop(object? sender, DebuggerStopEventArgs e)
    {
        var debugger = _debugger;

        if (debugger is null)
        {
            e.ResumeAction = DebuggerResumeAction.Continue;
            return;
        }

        // The UI went away. Nobody is watching, so drop every breakpoint and let the script finish.
        if (IsDetached)
        {
            ClearDebuggingState(debugger);
            e.ResumeAction = DebuggerResumeAction.Continue;
            return;
        }

        // A breakpoint hit while evaluating a watch or console expression re-enters this handler on this
        // same thread, inside the pump below. Turning it into an immediate Continue gives the semantics
        // users expect: breakpoints do not fire inside watch expressions.
        if (Volatile.Read(ref _nestingDepth) > 0)
        {
            e.ResumeAction = DebuggerResumeAction.Continue;
            return;
        }

        // Step-into-on-start arms step mode before the wrapper pipeline runs, so the first stops land in
        // that sourceless wrapper. Keep stepping until execution reaches the script itself.
        if (_skipUntilScript)
        {
            if (!Scripting.ScriptPath.AreSame(e.InvocationInfo?.ScriptName, ScriptPath) && e.Breakpoints.Count == 0)
            {
                e.ResumeAction = DebuggerResumeAction.StepInto;
                return;
            }

            _skipUntilScript = false;
        }

        var pump = new BlockingCollection<DebuggerWorkItem>();
        Volatile.Write(ref _pump, pump);

        try
        {
            FlushPendingBreakpointChanges(debugger);

            Expander.Reset();
            var info = DebuggerStopInfo.Capture(e, debugger.GetCallStack());

            SetState(DebugSessionState.Stopped);

            // Fire and forget. Waiting on the UI here is the deadlock described on the class.
            Stopped?.Invoke(info);

            foreach (var item in pump.GetConsumingEnumerable())
            {
                if (item is ResumeWorkItem resume)
                {
                    e.ResumeAction = resume.Action;
                    resume.Acknowledge();
                    break;
                }

                item.Execute(this, debugger);

                // A console command like "c" is interpreted by the debugger itself, which reports the
                // resume through DebuggerCommandResults rather than through a work item.
                var requested = Interlocked.Exchange(ref _pumpResumeRequest, 0);

                if (requested != 0)
                {
                    e.ResumeAction = (DebuggerResumeAction)(requested - 1);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _sink.WriteLine(ConsoleOutputKind.Error, "Debugger error: " + ex.GetBaseException().Message);

            // Never leave the pipeline wedged in a stop nobody can resume.
            e.ResumeAction = DebuggerResumeAction.Stop;
        }
        finally
        {
            Volatile.Write(ref _pump, null);
            pump.CompleteAdding();

            // Anything queued but not serviced belongs to a UI await that would otherwise hang.
            foreach (var orphan in pump)
                orphan.Cancel(new InvalidOperationException("The debugger resumed before this completed."));

            pump.Dispose();

            Expander.Reset();
            SetState(DebugSessionState.Running);
            Resumed?.Invoke();
        }
    }

    private Task<TResult> Enqueue<TResult>(DebuggerWorkItem<TResult> item)
    {
        var pump = Volatile.Read(ref _pump);

        if (pump is null)
            return Task.FromException<TResult>(new InvalidOperationException("The debugger is not stopped."));

        try
        {
            pump.Add(item);
        }
        catch (Exception ex)
        {
            // The pump completed between the null check and the add: a legitimate race with the script
            // resuming. Faulting is correct; the caller renders it as a cancelled evaluation.
            return Task.FromException<TResult>(ex);
        }

        return item.Task;
    }

    internal void EnterNestedEvaluation()
    {
        Interlocked.Increment(ref _nestingDepth);
        SetState(DebugSessionState.Evaluating);
    }

    internal void ExitNestedEvaluation()
    {
        Interlocked.Decrement(ref _nestingDepth);
        SetState(DebugSessionState.Stopped);
    }

    internal void RequestResumeFromPump(DebuggerResumeAction action) =>
        Interlocked.Exchange(ref _pumpResumeRequest, (int)action + 1);

    // ---- UI-facing operations -----------------------------------------------------------

    /// <summary>Resume from a stop. Safe to call from the UI thread; never blocks.</summary>
    public void Resume(DebugResumeKind kind)
    {
        if (State is not (DebugSessionState.Stopped or DebugSessionState.Evaluating))
            return;

        _ = Enqueue(new ResumeWorkItem(ToResumeAction(kind)));
    }

    /// <summary>
    /// Terminate the run. Which mechanism is correct depends entirely on the current state, and using the
    /// wrong one is the classic way to hang the application.
    /// </summary>
    public void RequestStop()
    {
        switch (State)
        {
            case DebugSessionState.Stopped:
                // Parked in the pump. Go through it; setting ResumeAction out of band would race with the
                // handler that owns it.
                _ = Enqueue(new ResumeWorkItem(DebuggerResumeAction.Stop));
                break;

            case DebugSessionState.Evaluating:
                // A watch or console expression is running on the pipeline thread. This is a cancellation
                // signal and is safe cross-thread; it unwinds only the nested pipeline.
                TryStopProcessCommand();
                break;

            case DebugSessionState.Running:
            case DebugSessionState.Starting:
                SetState(DebugSessionState.Stopping);
                _cancellation?.Cancel();

                // BeginStop, not Stop: Stop blocks until the pipeline unwinds, which can be seconds inside
                // Start-Sleep or a native call.
                try
                {
                    _powerShell?.BeginStop(static result =>
                    {
                        try { (result.AsyncState as PowerShellInstance)?.EndStop(result); }
                        catch (Exception) { /* the pipeline was already gone */ }
                    }, _powerShell);
                }
                catch (Exception)
                {
                    // Already stopping or disposed.
                }
                break;

            case DebugSessionState.Stopping:
                // Second press: escalate. CancelDebuggerProcessing aborts debugger processing outright.
                if (Interlocked.Exchange(ref _stopEscalation, 1) == 0)
                {
                    try { _debugger?.CancelDebuggerProcessing(); }
                    catch (Exception) { }
                }
                break;
        }
    }

    /// <summary>
    /// Stop observing the run without ending it. Output is discarded from here on, a pending Read-Host is
    /// answered with an empty line, and every breakpoint and step is dropped so the script runs to
    /// completion. Used when the debugger window closes or the debug pipe is lost.
    /// </summary>
    public void Detach()
    {
        if (Interlocked.Exchange(ref _detached, 1) != 0)
            return;

        _sink.Detach();

        switch (State)
        {
            case DebugSessionState.Stopped:
                _ = Enqueue(new DetachWorkItem());
                break;

            case DebugSessionState.Evaluating:
                TryStopProcessCommand();
                _ = Enqueue(new DetachWorkItem());
                break;

            // Starting/Running: the next DebuggerStop sees the flag and continues.
        }
    }

    /// <summary>Evaluate a watch expression in the stopped debugger's context.</summary>
    public Task<EvaluationResult> EvaluateAsync(string expression, TimeSpan timeout) =>
        RunEvaluationAsync(new ProcessCommandWorkItem(expression, formatForDisplay: true), timeout);

    /// <summary>Run a line typed into the console while stopped.</summary>
    public Task<EvaluationResult> ExecuteConsoleCommandAsync(string command, TimeSpan timeout)
    {
        // Bare debugger commands (continue, step, stack, quit) are interpreted by the debugger itself.
        // Wrapping them in Out-String would hide them from it, so they go through unwrapped.
        var isDebuggerCommand = IsDebuggerCommand(command);

        return RunEvaluationAsync(new ProcessCommandWorkItem(command, formatForDisplay: !isDebuggerCommand), timeout);
    }

    private async Task<EvaluationResult> RunEvaluationAsync(ProcessCommandWorkItem item, TimeSpan timeout)
    {
        var task = Enqueue(item);

        try
        {
            var winner = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);

            if (winner != task)
            {
                TryStopProcessCommand();

                return new EvaluationResult(
                    "Evaluation timed out after " + timeout.TotalSeconds.ToString("0.#") + "s and was cancelled.",
                    IsError: true,
                    ResumeAction: null);
            }

            return await task.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return new EvaluationResult(ex.GetBaseException().Message, IsError: true, ResumeAction: null);
        }
    }

    /// <summary>Fetch the variables visible in a call-stack frame. Runs on the pipeline thread.</summary>
    public Task<IReadOnlyList<VariableInfo>> GetFrameVariablesAsync(int scope, TimeSpan timeout) =>
        RunQueryAsync(new FrameVariablesWorkItem(scope), timeout, (IReadOnlyList<VariableInfo>)Array.Empty<VariableInfo>());

    /// <summary>Fetch the children of a variable node. Runs on the pipeline thread via the pump.</summary>
    public Task<IReadOnlyList<VariableInfo>> ExpandVariableAsync(int handle, TimeSpan timeout) =>
        RunQueryAsync(new InspectWorkItem(handle), timeout, (IReadOnlyList<VariableInfo>)Array.Empty<VariableInfo>());

    /// <summary>
    /// Enumerate the commands this session can resolve. Only valid while stopped, which is the point: this
    /// is the one place the debugger can see modules the script imported at runtime.
    /// </summary>
    public Task<IReadOnlyList<CommandInfoSnapshot>> GetCommandsAsync(TimeSpan timeout) =>
        RunQueryAsync(new CommandListWorkItem(), timeout, (IReadOnlyList<CommandInfoSnapshot>)Array.Empty<CommandInfoSnapshot>());

    /// <summary>Describe one command in the stopped session's context.</summary>
    public Task<CommandDetail?> GetCommandDetailAsync(string name, TimeSpan timeout) =>
        RunQueryAsync<CommandDetail?>(new CommandDetailWorkItem(name), timeout, null);

    private async Task<TResult> RunQueryAsync<TResult>(DebuggerWorkItem<TResult> item, TimeSpan timeout, TResult fallback)
    {
        var task = Enqueue(item);

        try
        {
            var winner = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);

            if (winner != task)
            {
                TryStopProcessCommand();
                return fallback;
            }

            return await task.ConfigureAwait(false);
        }
        catch (Exception)
        {
            return fallback;
        }
    }

    /// <summary>
    /// Add or remove a breakpoint mid-run. While stopped it takes effect immediately; while running it is
    /// queued and applied at the next stop, because the UI thread must not call into the debugger.
    /// </summary>
    public void ChangeBreakpoint(BreakpointRequest request, bool add)
    {
        if (IsDetached)
            return;

        switch (State)
        {
            case DebugSessionState.Stopped:
                _ = Enqueue(new BreakpointChangeWorkItem(request, add));
                break;

            case DebugSessionState.Starting:
            case DebugSessionState.Running:
            case DebugSessionState.Evaluating:
                _pendingChanges.Enqueue(new PendingBreakpointChange(request, add));
                break;

            // Idle: nothing to do. The next Start applies the whole set.
        }
    }

    private void TryStopProcessCommand()
    {
        try { _debugger?.StopProcessCommand(); }
        catch (Exception) { }
    }

    // ---- breakpoints --------------------------------------------------------------------

    private void ApplyBreakpoints(SmaDebugger debugger, IReadOnlyList<BreakpointRequest> requests)
    {
        _breakpointIdToLine.Clear();
        _lineToBreakpoint.Clear();

        foreach (var request in requests)
        {
            try
            {
                ApplySingleBreakpointChange(debugger, request, add: true);
            }
            catch (Exception ex)
            {
                _sink.WriteLine(ConsoleOutputKind.System,
                    "[debugger] Could not set a breakpoint on line " + request.Line + ": " + ex.GetBaseException().Message);
            }
        }
    }

    internal void ApplySingleBreakpointChange(SmaDebugger debugger, BreakpointRequest request, bool add)
    {
        if (add)
        {
            if (_lineToBreakpoint.ContainsKey(request.Line))
                return;

            // Column 0 means "anywhere on this line". A non-zero column binds to one specific statement
            // and then silently never hits. Breakpoints always bind to this run's script, whatever path
            // the request carries, so a breakpoint set against a draft still fires in the installed file.
            var breakpoint = debugger.SetLineBreakpoint(ScriptPath, request.Line, 0, null);

            if (!request.Enabled)
                debugger.DisableBreakpoint(breakpoint);

            _lineToBreakpoint[request.Line] = breakpoint;
            _breakpointIdToLine[breakpoint.Id] = request.Line;
        }
        else
        {
            if (!_lineToBreakpoint.Remove(request.Line, out var breakpoint))
                return;

            _breakpointIdToLine.Remove(breakpoint.Id);
            debugger.RemoveBreakpoint(breakpoint);
        }
    }

    /// <summary>Drop every breakpoint and step mode. Pipeline thread only.</summary>
    internal void ClearDebuggingState(SmaDebugger debugger)
    {
        while (_pendingChanges.TryDequeue(out _)) { }

        _skipUntilScript = false;

        try { debugger.SetDebuggerStepMode(false); }
        catch (Exception) { }

        foreach (var breakpoint in _lineToBreakpoint.Values.ToArray())
        {
            try { debugger.RemoveBreakpoint(breakpoint); }
            catch (Exception) { }
        }

        _lineToBreakpoint.Clear();
        _breakpointIdToLine.Clear();
    }

    private void FlushPendingBreakpointChanges(SmaDebugger debugger)
    {
        while (_pendingChanges.TryDequeue(out var change))
        {
            try
            {
                ApplySingleBreakpointChange(debugger, change.Request, change.Add);
            }
            catch (Exception ex)
            {
                _sink.WriteLine(ConsoleOutputKind.System,
                    "[debugger] Could not update the breakpoint on line " + change.Request.Line + ": " + ex.GetBaseException().Message);
            }
        }
    }

    private void OnBreakpointUpdated(object? sender, BreakpointUpdatedEventArgs e)
    {
        // Pipeline thread. The UI marshals before touching observable collections.
        var breakpoint = e.Breakpoint;

        if (breakpoint is null)
            return;

        var line = _breakpointIdToLine.TryGetValue(breakpoint.Id, out var known)
            ? known
            : (breakpoint as LineBreakpoint)?.Line ?? 0;

        BreakpointChanged?.Invoke(new BreakpointUpdate(breakpoint.Id, line, e.UpdateType, breakpoint.HitCount));
    }

    // ---- output -------------------------------------------------------------------------

    private void HookStreams(PowerShellInstance powerShell, PSDataCollection<PSObject> output)
    {
        // Every one of these fires on the pipeline thread. Sinks queue and return rather than touching
        // the UI directly.
        output.DataAdded += (_, e) =>
        {
            var item = output[e.Index];

            if (item is not null)
                _sink.WriteLine(ConsoleOutputKind.Output, item.ToString());
        };

        powerShell.Streams.Error.DataAdded += (_, e) =>
        {
            var record = powerShell.Streams.Error[e.Index];

            if (record is not null)
                _sink.WriteLine(ConsoleOutputKind.Error, FormatErrorRecord(record));
        };

        powerShell.Streams.Warning.DataAdded += (_, e) =>
        {
            var record = powerShell.Streams.Warning[e.Index];

            if (record is not null)
                _sink.WriteLine(ConsoleOutputKind.Warning, record.Message);
        };

        powerShell.Streams.Verbose.DataAdded += (_, e) =>
        {
            var record = powerShell.Streams.Verbose[e.Index];

            if (record is not null)
                _sink.WriteLine(ConsoleOutputKind.Verbose, record.Message);
        };

        powerShell.Streams.Debug.DataAdded += (_, e) =>
        {
            var record = powerShell.Streams.Debug[e.Index];

            if (record is not null)
                _sink.WriteLine(ConsoleOutputKind.Debug, record.Message);
        };

        powerShell.Streams.Information.DataAdded += (_, e) =>
        {
            var record = powerShell.Streams.Information[e.Index];

            if (record is null)
                return;

            // Write-Host already reached the console through the host UI; the engine also publishes it
            // here tagged PSHOST. Without this filter every Write-Host is doubled.
            if (record.Tags?.Contains("PSHOST") == true)
                return;

            _sink.WriteLine(ConsoleOutputKind.Information, record.MessageData?.ToString() ?? string.Empty);
        };

        powerShell.Streams.Progress.DataAdded += (_, e) =>
        {
            var record = powerShell.Streams.Progress[e.Index];

            if (record is not null)
                _sink.ReportProgress(0, ScriptProgress.From(record));
        };
    }

    private static string FormatErrorRecord(ErrorRecord record)
    {
        var text = record.ToString();
        var position = record.InvocationInfo?.PositionMessage;

        return string.IsNullOrWhiteSpace(position) ? text : text + Environment.NewLine + position;
    }

    private void ReportExecutionPolicyOnce()
    {
        if (!OperatingSystem.IsWindows() || Interlocked.Exchange(ref _executionPolicyReported, 1) != 0)
            return;

        try
        {
            using var probe = PowerShellInstance.Create();
            probe.Runspace = _runspace;
            probe.AddCommand("Get-ExecutionPolicy").AddParameter("List");

            foreach (var entry in probe.Invoke())
            {
                var scope = entry.Properties["Scope"]?.Value?.ToString();
                var policy = entry.Properties["ExecutionPolicy"]?.Value?.ToString();

                // Group Policy outranks the process scope, so a restrictive machine or user policy means
                // scripts may fail no matter what was asked for. Say so up front.
                if (scope is "MachinePolicy" or "UserPolicy" &&
                    policy is not null and not "Undefined" and not "Bypass" and not "Unrestricted")
                {
                    _sink.WriteLine(ConsoleOutputKind.System,
                        "[debugger] " + scope + " sets ExecutionPolicy to " + policy
                        + ", which overrides LANCommander. Unsigned scripts may fail to run.");
                }
            }
        }
        catch (Exception)
        {
            // Diagnostics only.
        }
    }

    // ---- teardown -----------------------------------------------------------------------

    /// <summary>
    /// Stop the run and wait for the pipeline to unwind. Disposing a runspace while the debugger is
    /// stopped hangs forever, so the order here is fixed.
    /// </summary>
    public void Shutdown(TimeSpan timeout)
    {
        if (State is DebugSessionState.Idle)
            return;

        RequestStop();

        if (_pipelineCompleted.Wait(timeout))
            return;

        // Did not unwind. Escalate, give it a moment, then give up: the pipeline thread is a background
        // thread, so an abandoned one does not keep the process alive.
        try { _debugger?.CancelDebuggerProcessing(); }
        catch (Exception) { }

        _pipelineCompleted.Wait(TimeSpan.FromSeconds(2));
    }

    private void CleanupEngine()
    {
        var debugger = _debugger;

        if (debugger is not null)
        {
            try
            {
                debugger.DebuggerStop -= OnDebuggerStop;
                debugger.BreakpointUpdated -= OnBreakpointUpdated;
            }
            catch (Exception) { }
        }

        _debugger = null;

        try { _powerShell?.Dispose(); } catch (Exception) { }
        _powerShell = null;

        try
        {
            if (_runspace is not null)
            {
                _runspace.Close();
                _runspace.Dispose();
            }
        }
        catch (Exception) { }

        _runspace = null;

        try { _cancellation?.Dispose(); } catch (Exception) { }
        _cancellation = null;

        _breakpointIdToLine.Clear();
        _lineToBreakpoint.Clear();
        _skipUntilScript = false;
        Expander.Reset();
    }

    public void Dispose()
    {
        Shutdown(TimeSpan.FromSeconds(5));
        _pipelineCompleted.Dispose();
    }

    // ---- helpers ------------------------------------------------------------------------

    private void SetState(DebugSessionState state)
    {
        if (Interlocked.Exchange(ref _state, (int)state) == (int)state)
            return;

        StateChanged?.Invoke(state);
    }

    private static DebuggerResumeAction ToResumeAction(DebugResumeKind kind) => kind switch
    {
        DebugResumeKind.Continue => DebuggerResumeAction.Continue,
        DebugResumeKind.StepOver => DebuggerResumeAction.StepOver,
        DebugResumeKind.StepInto => DebuggerResumeAction.StepInto,
        DebugResumeKind.StepOut => DebuggerResumeAction.StepOut,
        DebugResumeKind.Stop => DebuggerResumeAction.Stop,
        _ => DebuggerResumeAction.Continue,
    };

    private static bool IsDebuggerCommand(string command) => command.Trim().ToLowerInvariant() switch
    {
        "c" or "continue" or "s" or "stepinto" or "v" or "stepover" or "o" or "stepout"
            or "q" or "quit" or "k" or "get-pscallstack" or "l" or "list" or "h" or "?" => true,
        _ => false,
    };

    private readonly record struct PendingBreakpointChange(BreakpointRequest Request, bool Add);
}
