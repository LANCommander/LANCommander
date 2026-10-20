namespace LANCommander.Launcher.Services.Platform;

/// <summary>
/// Snapshot of the system power/battery state.
/// </summary>
/// <param name="HasBattery">False when the device has no battery (e.g. a desktop HTPC).</param>
/// <param name="Percent">Remaining charge, 0-100.</param>
/// <param name="IsCharging">True when the battery is charging.</param>
/// <param name="IsOnAc">True when running on AC power.</param>
public readonly record struct BatteryStatus(bool HasBattery, int Percent, bool IsCharging, bool IsOnAc);

/// <summary>
/// Reads the system battery state. Implementations are platform-specific and selected in DI.
/// </summary>
public interface IBatteryService
{
    BatteryStatus GetStatus();
}
