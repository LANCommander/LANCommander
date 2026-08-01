using System;
using System.IO;
using System.Linq;

namespace LANCommander.Launcher.Services.Platform;

/// <summary>
/// Reads battery state on Linux from the sysfs power_supply interface
/// (/sys/class/power_supply). Pure file reads, no shell-out.
/// </summary>
public sealed class LinuxBatteryService : IBatteryService
{
    private const string PowerSupplyRoot = "/sys/class/power_supply";

    public BatteryStatus GetStatus()
    {
        try
        {
            if (!Directory.Exists(PowerSupplyRoot))
                return new BatteryStatus(false, 0, false, true);

            var supplies = Directory.GetDirectories(PowerSupplyRoot);

            // AC adapter: "online" == 1 means plugged in.
            var isOnAc = supplies
                .Where(d => ReadType(d) is "Mains" or "USB")
                .Select(d => ReadInt(Path.Combine(d, "online")))
                .Any(online => online == 1);

            // First battery-type supply.
            var battery = supplies.FirstOrDefault(d => ReadType(d) == "Battery");
            if (battery is null)
                return new BatteryStatus(false, 0, false, isOnAc);

            var percent = ReadInt(Path.Combine(battery, "capacity")) ?? 0;
            var statusText = ReadText(Path.Combine(battery, "status"));
            var isCharging = string.Equals(statusText, "Charging", StringComparison.OrdinalIgnoreCase);

            // If there's no explicit AC supply, infer AC presence from charging/full state.
            if (!isOnAc)
                isOnAc = isCharging || string.Equals(statusText, "Full", StringComparison.OrdinalIgnoreCase);

            return new BatteryStatus(true, Math.Clamp(percent, 0, 100), isCharging, isOnAc);
        }
        catch
        {
            return new BatteryStatus(false, 0, false, true);
        }
    }

    private static string? ReadType(string dir)
    {
        var value = ReadText(Path.Combine(dir, "type"));
        return value;
    }

    private static string? ReadText(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    private static int? ReadInt(string path)
    {
        var text = ReadText(path);
        return int.TryParse(text, out var value) ? value : null;
    }
}
