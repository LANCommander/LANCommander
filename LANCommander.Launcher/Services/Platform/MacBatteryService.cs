using System;
using System.Text.RegularExpressions;

namespace LANCommander.Launcher.Services.Platform;

/// <summary>
/// Reads battery state on macOS by parsing the output of "pmset -g batt".
/// </summary>
public sealed partial class MacBatteryService : IBatteryService
{
    public BatteryStatus GetStatus()
    {
        var output = ProcessHelper.Run("/usr/bin/pmset", "-g batt");

        if (string.IsNullOrWhiteSpace(output))
            return new BatteryStatus(false, 0, false, true);

        // Example line: " -InternalBattery-0 (id=...)   83%; discharging; 4:12 remaining present: true"
        var match = PercentRegex().Match(output);
        if (!match.Success)
            return new BatteryStatus(false, 0, false, true);

        var percent = int.TryParse(match.Groups[1].Value, out var value) ? Math.Clamp(value, 0, 100) : 0;
        var isCharging = output.Contains("charging", StringComparison.OrdinalIgnoreCase)
                         && !output.Contains("discharging", StringComparison.OrdinalIgnoreCase);
        var isOnAc = output.Contains("AC Power", StringComparison.OrdinalIgnoreCase)
                     || output.Contains("charged", StringComparison.OrdinalIgnoreCase)
                     || isCharging;

        return new BatteryStatus(true, percent, isCharging, isOnAc);
    }

    [GeneratedRegex(@"(\d{1,3})%")]
    private static partial Regex PercentRegex();
}
