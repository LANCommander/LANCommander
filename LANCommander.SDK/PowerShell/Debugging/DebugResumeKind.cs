namespace LANCommander.SDK.PowerShell.Debugging;

/// <summary>UI-facing verbs, mapped to <c>DebuggerResumeAction</c> inside the session.</summary>
public enum DebugResumeKind
{
    Continue,
    StepOver,
    StepInto,
    StepOut,
    Stop,
}
