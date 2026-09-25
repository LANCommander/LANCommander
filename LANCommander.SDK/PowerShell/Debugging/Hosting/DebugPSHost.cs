#nullable enable
using System;
using System.Globalization;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Threading;

namespace LANCommander.SDK.PowerShell.Debugging.Hosting;

/// <summary>
/// The PSHost a debugged script's runspace runs under. The per-run state (<see cref="LastExitCode"/>, the
/// input cancellation token) is reset by <see cref="DebugSession"/> when a run starts.
/// </summary>
public sealed class DebugPSHost : PSHost
{
    private readonly DebugPSHostUserInterface _ui;
    private readonly HostPrivateData _privateData = new();
    private readonly Guid _instanceId = Guid.NewGuid();
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;

    public DebugPSHost(IConsoleSink sink) => _ui = new DebugPSHostUserInterface(sink);

    public override string Name => "LANCommander";

    public override Version Version { get; } =
        typeof(DebugPSHost).Assembly.GetName().Version ?? new Version(1, 0, 0, 0);

    public override Guid InstanceId => _instanceId;

    public override PSHostUserInterface UI => _ui;

    // Returning null from either of these throws deep inside the engine's formatting code with an
    // unhelpful stack, so they are always populated.
    public override CultureInfo CurrentCulture => _culture;

    public override CultureInfo CurrentUICulture => _uiCulture;

    public override PSObject PrivateData => PSObject.AsPSObject(_privateData);

    /// <summary>The exit code from the script's last <c>exit</c> statement, or null if it did not call one.</summary>
    public int? LastExitCode { get; private set; }

    /// <summary>Cleared by <see cref="DebugSession"/> at the start of every run.</summary>
    public void ResetPerRunState()
    {
        LastExitCode = null;
        _ui.InputCancellation = CancellationToken.None;
    }

    public void SetInputCancellation(CancellationToken token) => _ui.InputCancellation = token;

    /// <summary>
    /// Records the code and returns. It must NOT exit the process: a script's <c>exit 1</c> would
    /// otherwise close the launcher.
    /// </summary>
    public override void SetShouldExit(int exitCode) => LastExitCode = exitCode;

    // Breakpoints are surfaced through the debugger pump rather than the host's nested-prompt mechanism.
    public override void EnterNestedPrompt() { }

    public override void ExitNestedPrompt() { }

    // Called around native-executable invocations so a console host can restore its buffer. There is no
    // real console here.
    public override void NotifyBeginApplication() { }

    public override void NotifyEndApplication() { }
}
