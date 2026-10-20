namespace LANCommander.Server.UI.Components;

/// <summary>Where a script run shown in a <see cref="PowerShellConsole"/> is.</summary>
public enum ConsoleRunState
{
    Idle,
    Running,
    Completed,
    Stopped,
}
