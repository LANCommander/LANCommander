using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using LANCommander.SDK.PowerShell.Debugging;
using LANCommander.SDK.PowerShell.Debugging.Commands;

namespace LANCommander.Launcher.ViewModels.ScriptDebugger;

/// <summary>
/// The one place a debugger window crosses from the engine's threads to the UI thread. Lives as long as
/// the window; each script run binds its session here, local or in an elevated process.
/// </summary>
/// <remarks>
/// <para>
/// Session events arrive on the pipeline thread (or the debug pipe's reader) and are re-raised on the
/// UI thread with <c>Dispatcher.UIThread.Post</c>. Post, never <c>InvokeAsync(...).Wait()</c>: the stop
/// handler has not begun consuming its pump when it raises Stopped, so a UI delegate that needed the
/// debugger would enqueue work only that blocked thread can service.
/// </para>
/// <para>
/// Events from a session that has since been replaced are dropped, so a late event from one run can
/// never land in the next.
/// </para>
/// </remarks>
public sealed class DebugSessionController
{
    private static readonly TimeSpan EvaluationTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Command enumeration gets its own budget: an unfiltered <c>Get-Command</c> against a cold
    /// command-discovery cache walks every module on PSModulePath.
    /// </summary>
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(30);

    private IDebugSessionHandle? _session;

    // All raised on the UI thread.
    public event Action<IDebugSessionHandle>? SessionBound;
    public event Action<DebuggerStopInfo>? Stopped;
    public event Action? Resumed;
    public event Action<DebugSessionState>? StateChanged;
    public event Action<BreakpointUpdate>? BreakpointChanged;
    public event Action<RunCompletion>? RunCompleted;

    public IDebugSessionHandle? Session => Volatile.Read(ref _session);

    public DebugSessionState State => Session?.State ?? DebugSessionState.Idle;

    public bool IsBusy => State is not DebugSessionState.Idle;

    public string ScriptPath => Session?.ScriptPath ?? string.Empty;

    /// <summary>
    /// Attach to a run. Called on the thread about to run the script, before the session starts, so no
    /// event can be missed; it only subscribes and posts.
    /// </summary>
    public void Bind(IDebugSessionHandle session)
    {
        Volatile.Write(ref _session, session);

        session.Stopped += info => Post(session, () => Stopped?.Invoke(info));
        session.Resumed += () => Post(session, () => Resumed?.Invoke());
        session.StateChanged += state => Post(session, () => StateChanged?.Invoke(state));
        session.BreakpointChanged += update => Post(session, () => BreakpointChanged?.Invoke(update));
        session.RunCompleted += completion => Post(session, () =>
        {
            Interlocked.CompareExchange(ref _session, null, session);
            RunCompleted?.Invoke(completion);
        });

        Post(session, () => SessionBound?.Invoke(session));
    }

    private void Post(IDebugSessionHandle session, Action action) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (ReferenceEquals(Volatile.Read(ref _session), session))
                action();
        });

    public void Resume(DebugResumeKind kind) => Session?.Resume(kind);

    public void RequestStop() => Session?.RequestStop();

    public void ChangeBreakpoint(BreakpointRequest request, bool add) => Session?.ChangeBreakpoint(request, add);

    /// <summary>Let the current run finish unobserved. Used when the window closes.</summary>
    public void Detach()
    {
        var session = Interlocked.Exchange(ref _session, null);

        session?.Detach();
    }

    public Task<EvaluationResult> EvaluateAsync(string expression) =>
        Session?.EvaluateAsync(expression, EvaluationTimeout) ?? NotStopped();

    public Task<EvaluationResult> ExecuteConsoleCommandAsync(string command) =>
        Session?.ExecuteConsoleCommandAsync(command, EvaluationTimeout) ?? NotStopped();

    public Task<IReadOnlyList<VariableInfo>> GetFrameVariablesAsync(int scope) =>
        Session?.GetFrameVariablesAsync(scope, EvaluationTimeout) ?? Task.FromResult<IReadOnlyList<VariableInfo>>([]);

    public Task<IReadOnlyList<VariableInfo>> ExpandVariableAsync(int handle) =>
        Session?.ExpandVariableAsync(handle, EvaluationTimeout) ?? Task.FromResult<IReadOnlyList<VariableInfo>>([]);

    /// <summary>Commands visible to the stopped session, including modules the script imported.</summary>
    public Task<IReadOnlyList<CommandInfoSnapshot>> GetCommandsAsync() =>
        Session?.GetCommandsAsync(CommandTimeout) ?? Task.FromResult<IReadOnlyList<CommandInfoSnapshot>>([]);

    public Task<CommandDetail?> GetCommandDetailAsync(string name) =>
        Session?.GetCommandDetailAsync(name, EvaluationTimeout) ?? Task.FromResult<CommandDetail?>(null);

    private static Task<EvaluationResult> NotStopped() =>
        Task.FromResult(new EvaluationResult("No script is being debugged.", IsError: true, ResumeAction: null));
}
