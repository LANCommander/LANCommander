#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK.PowerShell.Debugging.Commands;

namespace LANCommander.SDK.PowerShell.Debugging.Remote;

/// <summary>
/// The launcher's view of a script being debugged in an elevated child process. Every operation is a
/// message over the debug pipe; every event arrives from the pipe's read loop. To the debugger UI it is
/// indistinguishable from a local <see cref="DebugSession"/>.
/// </summary>
public sealed class RemoteDebugSession : IDebugSessionHandle
{
    private readonly DebugConnection _connection;
    private int _state = (int)DebugSessionState.Starting;
    private int _completed;

    internal RemoteDebugSession(DebugConnection connection, string scriptPath, int processId)
    {
        _connection = connection;
        ScriptPath = scriptPath;
        ProcessId = processId;
    }

    public string ScriptPath { get; }

    public DebugSessionState State => (DebugSessionState)Volatile.Read(ref _state);

    public bool IsRemote => true;

    public int? ProcessId { get; }

    public event Action<DebuggerStopInfo>? Stopped;

    public event Action? Resumed;

    public event Action<DebugSessionState>? StateChanged;

    public event Action<BreakpointUpdate>? BreakpointChanged;

    public event Action<RunCompletion>? RunCompleted;

    public void Resume(DebugResumeKind kind) => _connection.Post(new ResumeCommand { Kind = kind });

    public void RequestStop() => _connection.Post(new StopCommand());

    public void Detach() => _connection.Post(new DetachCommand());

    public void ChangeBreakpoint(BreakpointRequest request, bool add) =>
        _connection.Post(new ChangeBreakpointCommand { Request = request, Add = add });

    public async Task<EvaluationResult> EvaluateAsync(string expression, TimeSpan timeout) =>
        await EvaluateCoreAsync(expression, isConsoleCommand: false, timeout).ConfigureAwait(false);

    public async Task<EvaluationResult> ExecuteConsoleCommandAsync(string command, TimeSpan timeout) =>
        await EvaluateCoreAsync(command, isConsoleCommand: true, timeout).ConfigureAwait(false);

    private async Task<EvaluationResult> EvaluateCoreAsync(string expression, bool isConsoleCommand, TimeSpan timeout)
    {
        var response = await _connection.RequestAsync<EvaluateResponse>(
            new EvaluateRequest
            {
                Expression = expression,
                IsConsoleCommand = isConsoleCommand,
                TimeoutMilliseconds = ToMilliseconds(timeout),
            },
            timeout + DebugProtocol.ReplyGrace).ConfigureAwait(false);

        return response?.Result
            ?? new EvaluationResult("The elevated process did not answer.", IsError: true, ResumeAction: null);
    }

    public async Task<IReadOnlyList<VariableInfo>> GetFrameVariablesAsync(int scope, TimeSpan timeout)
    {
        var response = await _connection.RequestAsync<VariablesResponse>(
            new FrameVariablesRequest { Scope = scope, TimeoutMilliseconds = ToMilliseconds(timeout) },
            timeout + DebugProtocol.ReplyGrace).ConfigureAwait(false);

        return (IReadOnlyList<VariableInfo>?)response?.Variables ?? Array.Empty<VariableInfo>();
    }

    public async Task<IReadOnlyList<VariableInfo>> ExpandVariableAsync(int handle, TimeSpan timeout)
    {
        var response = await _connection.RequestAsync<VariablesResponse>(
            new ExpandVariableRequest { Handle = handle, TimeoutMilliseconds = ToMilliseconds(timeout) },
            timeout + DebugProtocol.ReplyGrace).ConfigureAwait(false);

        return (IReadOnlyList<VariableInfo>?)response?.Variables ?? Array.Empty<VariableInfo>();
    }

    public async Task<IReadOnlyList<CommandInfoSnapshot>> GetCommandsAsync(TimeSpan timeout)
    {
        var response = await _connection.RequestAsync<CommandsResponse>(
            new CommandsRequest { TimeoutMilliseconds = ToMilliseconds(timeout) },
            timeout + DebugProtocol.ReplyGrace).ConfigureAwait(false);

        return (IReadOnlyList<CommandInfoSnapshot>?)response?.Commands ?? Array.Empty<CommandInfoSnapshot>();
    }

    public async Task<CommandDetail?> GetCommandDetailAsync(string name, TimeSpan timeout)
    {
        var response = await _connection.RequestAsync<CommandDetailResponse>(
            new CommandDetailRequest { Name = name, TimeoutMilliseconds = ToMilliseconds(timeout) },
            timeout + DebugProtocol.ReplyGrace).ConfigureAwait(false);

        return response?.Detail;
    }

    // ---- inbound events, called from the pipe's read loop ---------------------------------

    internal void OnStateChanged(DebugSessionState state)
    {
        Volatile.Write(ref _state, (int)state);
        StateChanged?.Invoke(state);
    }

    internal void OnStopped(DebuggerStopInfo info) => Stopped?.Invoke(info);

    internal void OnResumed() => Resumed?.Invoke();

    internal void OnBreakpointChanged(BreakpointUpdate update) => BreakpointChanged?.Invoke(update);

    internal void OnRunCompleted(RunCompletion completion)
    {
        if (Interlocked.Exchange(ref _completed, 1) != 0)
            return;

        Volatile.Write(ref _state, (int)DebugSessionState.Idle);
        StateChanged?.Invoke(DebugSessionState.Idle);
        RunCompleted?.Invoke(completion);
    }

    /// <summary>The pipe dropped mid-run. The child detaches and carries on; the UI sees the run end.</summary>
    internal void OnDisconnected() =>
        OnRunCompleted(new RunCompletion(null, true, "The elevated process disconnected from the debugger.", TimeSpan.Zero));

    private static int ToMilliseconds(TimeSpan timeout) =>
        (int)Math.Clamp(timeout.TotalMilliseconds, 0, int.MaxValue);
}
