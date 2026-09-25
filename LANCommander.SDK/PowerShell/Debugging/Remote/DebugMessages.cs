#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using LANCommander.SDK.PowerShell.Debugging.Commands;
using LANCommander.SDK.PowerShell.Debugging.Hosting;

namespace LANCommander.SDK.PowerShell.Debugging.Remote;

/// <summary>
/// Everything that crosses the debug pipe between the launcher (which owns the debugger window) and an
/// elevated child process (which runs the script and the debug engine).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(HelloMessage), "hello")]
[JsonDerivedType(typeof(HelloAckMessage), "helloAck")]
[JsonDerivedType(typeof(AttachRequest), "attach")]
[JsonDerivedType(typeof(AttachResponse), "attachResponse")]
[JsonDerivedType(typeof(PrepareScriptFileRequest), "prepare")]
[JsonDerivedType(typeof(AckResponse), "ack")]
[JsonDerivedType(typeof(OutputMessage), "output")]
[JsonDerivedType(typeof(ProgressMessage), "progress")]
[JsonDerivedType(typeof(ReadLineRequest), "readLine")]
[JsonDerivedType(typeof(ReadLineResponse), "readLineResponse")]
[JsonDerivedType(typeof(ReadLineCancelMessage), "readLineCancel")]
[JsonDerivedType(typeof(StateChangedEvent), "state")]
[JsonDerivedType(typeof(StoppedEvent), "stopped")]
[JsonDerivedType(typeof(ResumedEvent), "resumed")]
[JsonDerivedType(typeof(BreakpointChangedEvent), "breakpoint")]
[JsonDerivedType(typeof(RunCompletedEvent), "completed")]
[JsonDerivedType(typeof(ResumeCommand), "resume")]
[JsonDerivedType(typeof(StopCommand), "stop")]
[JsonDerivedType(typeof(DetachCommand), "detach")]
[JsonDerivedType(typeof(ChangeBreakpointCommand), "changeBreakpoint")]
[JsonDerivedType(typeof(EvaluateRequest), "evaluate")]
[JsonDerivedType(typeof(EvaluateResponse), "evaluateResponse")]
[JsonDerivedType(typeof(FrameVariablesRequest), "frameVariables")]
[JsonDerivedType(typeof(ExpandVariableRequest), "expandVariable")]
[JsonDerivedType(typeof(VariablesResponse), "variablesResponse")]
[JsonDerivedType(typeof(CommandsRequest), "commands")]
[JsonDerivedType(typeof(CommandsResponse), "commandsResponse")]
[JsonDerivedType(typeof(CommandDetailRequest), "commandDetail")]
[JsonDerivedType(typeof(CommandDetailResponse), "commandDetailResponse")]
public abstract class DebugMessage;

/// <summary>A message that expects exactly one <see cref="DebugResponse"/> with the same id.</summary>
public abstract class DebugRequest : DebugMessage
{
    public long RequestId { get; set; }
}

public abstract class DebugResponse : DebugMessage
{
    public long RequestId { get; set; }
}

// ---- handshake (child → parent, parent → child) --------------------------------------------

public sealed class HelloMessage : DebugMessage
{
    public int ProtocolVersion { get; set; }
    public string Token { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public bool IsElevated { get; set; }
}

public sealed class HelloAckMessage : DebugMessage
{
    public bool Accepted { get; set; }
    public string? Reason { get; set; }
}

// ---- broker (child → parent) ---------------------------------------------------------------

public sealed class AttachRequest : DebugRequest
{
    public ScriptIdentity Identity { get; set; } = null!;
}

public sealed class AttachResponse : DebugResponse
{
    public bool Accepted { get; set; }
    public List<BreakpointRequest> Breakpoints { get; set; } = new();
    public bool StepIntoOnStart { get; set; }
}

public sealed class PrepareScriptFileRequest : DebugRequest
{
    public ScriptIdentity Identity { get; set; } = null!;
    public string Path { get; set; } = string.Empty;
}

public sealed class AckResponse : DebugResponse;

// ---- console (child → parent, with Read-Host answered parent → child) ----------------------

public sealed class OutputMessage : DebugMessage
{
    public ConsoleOutputKind Kind { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool NewLine { get; set; }
    public ConsoleColor? Foreground { get; set; }
    public ConsoleColor? Background { get; set; }
}

public sealed class ProgressMessage : DebugMessage
{
    public long SourceId { get; set; }
    public ScriptProgress Progress { get; set; } = null!;
}

public sealed class ReadLineRequest : DebugRequest
{
    public string? Prompt { get; set; }
    public bool Secure { get; set; }
}

public sealed class ReadLineResponse : DebugResponse
{
    public string? Text { get; set; }
    public bool Cancelled { get; set; }
}

/// <summary>The child stopped waiting for an answer (the script was stopped).</summary>
public sealed class ReadLineCancelMessage : DebugMessage
{
    public long RequestId { get; set; }
}

// ---- session events (child → parent) ------------------------------------------------------

public sealed class StateChangedEvent : DebugMessage
{
    public DebugSessionState State { get; set; }
}

public sealed class StoppedEvent : DebugMessage
{
    public DebuggerStopInfo Info { get; set; } = null!;
}

public sealed class ResumedEvent : DebugMessage;

public sealed class BreakpointChangedEvent : DebugMessage
{
    public BreakpointUpdate Update { get; set; }
}

public sealed class RunCompletedEvent : DebugMessage
{
    public int? ExitCode { get; set; }
    public bool Faulted { get; set; }
    public string? ErrorMessage { get; set; }
    public long DurationMilliseconds { get; set; }
}

// ---- session control (parent → child) -----------------------------------------------------

public sealed class ResumeCommand : DebugMessage
{
    public DebugResumeKind Kind { get; set; }
}

public sealed class StopCommand : DebugMessage;

public sealed class DetachCommand : DebugMessage;

public sealed class ChangeBreakpointCommand : DebugMessage
{
    public BreakpointRequest Request { get; set; }
    public bool Add { get; set; }
}

public sealed class EvaluateRequest : DebugRequest
{
    public string Expression { get; set; } = string.Empty;
    public bool IsConsoleCommand { get; set; }
    public int TimeoutMilliseconds { get; set; }
}

public sealed class EvaluateResponse : DebugResponse
{
    public EvaluationResult Result { get; set; } = null!;
}

public sealed class FrameVariablesRequest : DebugRequest
{
    public int Scope { get; set; }
    public int TimeoutMilliseconds { get; set; }
}

public sealed class ExpandVariableRequest : DebugRequest
{
    public int Handle { get; set; }
    public int TimeoutMilliseconds { get; set; }
}

public sealed class VariablesResponse : DebugResponse
{
    public List<VariableInfo> Variables { get; set; } = new();
}

public sealed class CommandsRequest : DebugRequest
{
    public int TimeoutMilliseconds { get; set; }
}

public sealed class CommandsResponse : DebugResponse
{
    public List<CommandInfoSnapshot> Commands { get; set; } = new();
}

public sealed class CommandDetailRequest : DebugRequest
{
    public string Name { get; set; } = string.Empty;
    public int TimeoutMilliseconds { get; set; }
}

public sealed class CommandDetailResponse : DebugResponse
{
    public CommandDetail? Detail { get; set; }
}
