using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace LANCommander.Launcher.Services.Platform;

/// <summary>
/// Reads/writes the default audio sink volume on Linux. Prefers wpctl (PipeWire / WirePlumber,
/// as used on the Steam Deck) and falls back to pactl (PulseAudio).
/// </summary>
public sealed partial class LinuxVolumeService : IVolumeService
{
    private enum Backend { None, WirePlumber, PulseAudio }

    private readonly Backend _backend;

    public LinuxVolumeService()
    {
        if (ProcessHelper.CommandExists("wpctl"))
            _backend = Backend.WirePlumber;
        else if (ProcessHelper.CommandExists("pactl"))
            _backend = Backend.PulseAudio;
        else
            _backend = Backend.None;
    }

    public bool IsSupported => _backend != Backend.None;

    public int GetVolume()
    {
        switch (_backend)
        {
            case Backend.WirePlumber:
            {
                // "Volume: 0.75" or "Volume: 0.75 [MUTED]"
                var output = ProcessHelper.Run("wpctl", "get-volume @DEFAULT_AUDIO_SINK@");
                var match = WpctlVolumeRegex().Match(output ?? string.Empty);
                if (match.Success && float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var scalar))
                    return Math.Clamp((int)Math.Round(scalar * 100), 0, 100);
                return 0;
            }
            case Backend.PulseAudio:
            {
                // "... front-left: 49152 /  75% / -7.50 dB, ..."
                var output = ProcessHelper.Run("pactl", "get-sink-volume @DEFAULT_SINK@");
                var match = PactlPercentRegex().Match(output ?? string.Empty);
                if (match.Success && int.TryParse(match.Groups[1].Value, out var percent))
                    return Math.Clamp(percent, 0, 100);
                return 0;
            }
            default:
                return 0;
        }
    }

    public void SetVolume(int percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        switch (_backend)
        {
            case Backend.WirePlumber:
                ProcessHelper.Run("wpctl", $"set-volume @DEFAULT_AUDIO_SINK@ {percent}%");
                break;
            case Backend.PulseAudio:
                ProcessHelper.Run("pactl", $"set-sink-volume @DEFAULT_SINK@ {percent}%");
                break;
        }
    }

    public bool GetMuted()
    {
        switch (_backend)
        {
            case Backend.WirePlumber:
            {
                var output = ProcessHelper.Run("wpctl", "get-volume @DEFAULT_AUDIO_SINK@");
                return output?.Contains("MUTED", StringComparison.OrdinalIgnoreCase) ?? false;
            }
            case Backend.PulseAudio:
            {
                var output = ProcessHelper.Run("pactl", "get-sink-mute @DEFAULT_SINK@");
                return output?.Contains("yes", StringComparison.OrdinalIgnoreCase) ?? false;
            }
            default:
                return false;
        }
    }

    [GeneratedRegex(@"Volume:\s*([0-9]*\.?[0-9]+)")]
    private static partial Regex WpctlVolumeRegex();

    [GeneratedRegex(@"/\s*(\d{1,3})%")]
    private static partial Regex PactlPercentRegex();
}
