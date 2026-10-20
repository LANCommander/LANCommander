using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace LANCommander.Launcher.Services.Platform;

/// <summary>
/// Reads battery state on Windows via kernel32!GetSystemPowerStatus. Uses only P/Invoke so it
/// compiles under the plain net10.0 TFM without WinRT.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsBatteryService : IBatteryService
{
    // SYSTEM_POWER_STATUS.BatteryFlag flags
    private const byte BatteryFlagCharging = 0x08;
    private const byte BatteryFlagNoBattery = 0x80;
    private const byte BatteryLifeUnknown = 0xFF;

    public BatteryStatus GetStatus()
    {
        if (!GetSystemPowerStatus(out var status))
            return new BatteryStatus(false, 0, false, true);

        var isOnAc = status.ACLineStatus == 1;
        var noBattery = (status.BatteryFlag & BatteryFlagNoBattery) != 0 || status.BatteryFlag == 0xFF;
        var isCharging = (status.BatteryFlag & BatteryFlagCharging) != 0;
        var percent = status.BatteryLifePercent == BatteryLifeUnknown ? 0 : status.BatteryLifePercent;

        return new BatteryStatus(!noBattery, percent, isCharging, isOnAc);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);
}
