using System;
using System.Collections.Generic;

namespace LANCommander.Launcher.Services.Platform;

/// <summary>
/// The devices on this machine that the Interposer can show to a game or hide from it, so a player can
/// pick them rather than type their names: network adapters, audio endpoints and DirectInput game
/// controllers. Each is described by the strings the Interposer's filters match on.
/// </summary>
public interface IDeviceCatalog
{
    IReadOnlyList<NetworkAdapterDevice> GetNetworkAdapters();

    IReadOnlyList<AudioDevice> GetAudioDevices();

    /// <summary>Attached DirectInput game controllers. Slow on a machine with many HID devices; call off the UI thread.</summary>
    IReadOnlyList<ControllerDevice> GetControllers();
}

/// <summary>A network adapter, as the Interposer's <c>NetworkAdapters</c> filter sees it.</summary>
/// <param name="Name">The adapter's friendly name, such as <c>Ethernet</c>.</param>
/// <param name="Description">Its description, usually the NIC model.</param>
/// <param name="MacAddress">Its physical address as <c>00:11:22:33:44:55</c>, or null when it has none.</param>
/// <param name="IPv4Address">Its first IPv4 address, or null.</param>
/// <param name="PrefixLength">The prefix length of that address's subnet, or null.</param>
/// <param name="IsConnected">Whether it is up.</param>
public sealed record NetworkAdapterDevice(
    string Name, string Description, string? MacAddress, string? IPv4Address, int? PrefixLength, bool IsConnected)
{
    /// <summary>
    /// The adapter's IPv4 subnet in CIDR form, such as <c>192.168.1.0/24</c>, or null. Null for a
    /// self-assigned 169.254 address too: every unconnected adapter has one, so as a rule it would let
    /// through every other unconnected adapter as well.
    /// </summary>
    public string? Subnet
    {
        get
        {
            if (IPv4Address == null || PrefixLength is not (>= 0 and <= 32) || !System.Net.IPAddress.TryParse(IPv4Address, out var address))
                return null;

            var bytes = address.GetAddressBytes();

            if (bytes.Length != 4 || bytes[0] == 169 && bytes[1] == 254)
                return null;

            var value = (uint)(bytes[0] << 24 | bytes[1] << 16 | bytes[2] << 8 | bytes[3]);
            var mask = PrefixLength == 0 ? 0u : uint.MaxValue << (32 - PrefixLength.Value);
            var network = value & mask;

            return $"{network >> 24}.{(network >> 16) & 0xFF}.{(network >> 8) & 0xFF}.{network & 0xFF}/{PrefixLength}";
        }
    }
}

public enum AudioDirection { Output, Input }

/// <summary>An audio endpoint, as the Interposer's <c>AudioDevices</c> filter sees it.</summary>
/// <param name="Id">The Core Audio endpoint id, which identifies it uniquely and survives renaming.</param>
/// <param name="Name">Its friendly name, such as <c>Speakers (Realtek High Definition Audio)</c>.</param>
/// <param name="Direction">Playback or recording.</param>
/// <param name="IsDefault">Whether it is the system default for its direction.</param>
public sealed record AudioDevice(string Id, string Name, AudioDirection Direction, bool IsDefault);

/// <summary>A DirectInput device, as the Interposer's <c>DirectInput.DeviceFilter</c> sees it.</summary>
/// <param name="InstanceName">Its instance name, such as <c>Controller (Xbox One For Windows)</c>.</param>
/// <param name="ProductName">Its product name.</param>
/// <param name="Class">Its device class, named as the filter's <c>Classes</c> are: <c>Gamepad</c>, <c>Joystick</c>, ...</param>
public sealed record ControllerDevice(string InstanceName, string ProductName, string Class);

/// <summary>For platforms the Interposer does not run on: there is nothing to filter.</summary>
public sealed class NullDeviceCatalog : IDeviceCatalog
{
    public IReadOnlyList<NetworkAdapterDevice> GetNetworkAdapters() => Array.Empty<NetworkAdapterDevice>();

    public IReadOnlyList<AudioDevice> GetAudioDevices() => Array.Empty<AudioDevice>();

    public IReadOnlyList<ControllerDevice> GetControllers() => Array.Empty<ControllerDevice>();
}
