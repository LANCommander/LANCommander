using System;

namespace LANCommander.Launcher.Services.Platform;

/// <summary>
/// Reads/writes the system output volume on macOS via osascript (AppleScript).
/// </summary>
public sealed class MacVolumeService : IVolumeService
{
    public bool IsSupported => true;

    public int GetVolume()
    {
        var output = ProcessHelper.Run("/usr/bin/osascript", "-e \"output volume of (get volume settings)\"");
        return int.TryParse(output, out var value) ? Math.Clamp(value, 0, 100) : 0;
    }

    public void SetVolume(int percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        ProcessHelper.Run("/usr/bin/osascript", $"-e \"set volume output volume {percent}\"");
    }

    public bool GetMuted()
    {
        var output = ProcessHelper.Run("/usr/bin/osascript", "-e \"output muted of (get volume settings)\"");
        return string.Equals(output, "true", StringComparison.OrdinalIgnoreCase);
    }
}
