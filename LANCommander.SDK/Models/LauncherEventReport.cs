using System;

namespace LANCommander.SDK.Models;

public enum LauncherEventType
{
    GameInstalled,
    GameInstallFailed,
    GameUninstalled,
    ToolInstalled,
    RedistributableInstalled,
}

public sealed class LauncherEventReport
{
    public LauncherEventType Type { get; set; }
    public Guid TargetId { get; set; }
}
