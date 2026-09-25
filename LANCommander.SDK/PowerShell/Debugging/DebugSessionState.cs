namespace LANCommander.SDK.PowerShell.Debugging;

public enum DebugSessionState
{
    /// <summary>No runspace.</summary>
    Idle,

    /// <summary>Runspace opening, breakpoints binding.</summary>
    Starting,

    /// <summary>Script executing; the debugger is not stopped.</summary>
    Running,

    /// <summary>Stopped at a breakpoint or step. The pump is servicing work items.</summary>
    Stopped,

    /// <summary>Stopped, and currently running a watch/console expression on the pipeline thread.</summary>
    Evaluating,

    /// <summary>Termination requested; waiting for the pipeline to unwind.</summary>
    Stopping,
}
