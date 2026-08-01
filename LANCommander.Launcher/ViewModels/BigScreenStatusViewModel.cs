using System;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using LANCommander.Launcher.Services.Platform;

namespace LANCommander.Launcher.ViewModels;

/// <summary>
/// Backs the big screen mode status items in the title bar (battery, volume, time).
/// Polls the platform services on a 1-second <see cref="DispatcherTimer"/> while running; time
/// refreshes every tick, battery/volume every few ticks. Only active in big screen mode.
/// </summary>
public partial class BigScreenStatusViewModel : ObservableObject
{
    private const int HardwarePollIntervalTicks = 5;

    private readonly IBatteryService _batteryService;
    private readonly IVolumeService _volumeService;
    private readonly DispatcherTimer _timer;

    private int _tickCounter;

    // Suppresses writing volume back to the OS while we're applying an OS-read value to the UI,
    // preventing a poll-in -> property-changed -> write-out feedback loop.
    private bool _suppressVolumeWrite;

    [ObservableProperty]
    private string _time = string.Empty;

    [ObservableProperty]
    private bool _hasBattery;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BatteryIcon))]
    private int _batteryPercent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BatteryIcon))]
    private bool _isCharging;

    [ObservableProperty]
    private bool _isVolumeSupported;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VolumeIcon))]
    private int _volume;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VolumeIcon))]
    private bool _isMuted;

    public string BatteryIcon
    {
        get
        {
            if (IsCharging)
                return "BatteryCharging";
            return BatteryPercent switch
            {
                >= 95 => "BatteryFull",
                >= 70 => "BatteryHigh",
                >= 40 => "BatteryMedium",
                >= 15 => "BatteryLow",
                >= 10 => "BatteryEmpty",
                _ => "BatteryWarning",
            };
        }
    }

    public string VolumeIcon
    {
        get
        {
            if (IsMuted || Volume == 0)
                return "SpeakerNone";
            return Volume <= 50 ? "SpeakerLow" : "SpeakerHigh";
        }
    }

    public BigScreenStatusViewModel(IBatteryService batteryService, IVolumeService volumeService)
    {
        _batteryService = batteryService;
        _volumeService = volumeService;

        IsVolumeSupported = volumeService.IsSupported;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Update();
    }

    /// <summary>Begins polling and does an immediate refresh. Safe to call when already running.</summary>
    public void Start()
    {
        if (_timer.IsEnabled)
            return;

        _tickCounter = 0;
        Update(forceHardware: true);
        _timer.Start();
    }

    /// <summary>Stops polling.</summary>
    public void Stop()
    {
        _timer.Stop();
    }

    private void Update(bool forceHardware = false)
    {
        Time = DateTime.Now.ToString(CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern, CultureInfo.CurrentCulture);

        if (!forceHardware && _tickCounter++ % HardwarePollIntervalTicks != 0)
            return;

        var battery = _batteryService.GetStatus();
        HasBattery = battery.HasBattery;
        BatteryPercent = battery.Percent;
        IsCharging = battery.IsCharging;

        if (IsVolumeSupported)
        {
            _suppressVolumeWrite = true;
            Volume = _volumeService.GetVolume();
            IsMuted = _volumeService.GetMuted();
            _suppressVolumeWrite = false;
        }
    }

    partial void OnVolumeChanged(int value)
    {
        if (_suppressVolumeWrite || !IsVolumeSupported)
            return;

        _volumeService.SetVolume(value);
    }
}
