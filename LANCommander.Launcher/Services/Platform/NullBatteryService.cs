namespace LANCommander.Launcher.Services.Platform;

/// <summary>
/// Fallback battery service used on unrecognized platforms. Reports no battery.
/// </summary>
public sealed class NullBatteryService : IBatteryService
{
    public BatteryStatus GetStatus() => new(HasBattery: false, Percent: 0, IsCharging: false, IsOnAc: true);
}
