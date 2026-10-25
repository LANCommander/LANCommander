#if DEBUG
using System.Collections.Generic;
using LANCommander.Launcher.Services.Platform;

namespace LANCommander.Launcher.Fixtures;

/// <summary>A machine's devices for the Interposer's device filters: a LAN party PC with a VPN and a headset.</summary>
public sealed class FixtureDeviceCatalog : IDeviceCatalog
{
    public const string SpeakersId = "{0.0.0.00000000}.{6d8a1c3e-2f4b-4a51-9c0e-3b7d5e9f1a20}";
    public const string EthernetMac = "58:11:22:4E:39:FA";

    public IReadOnlyList<NetworkAdapterDevice> GetNetworkAdapters() =>
    [
        new("Ethernet", "Intel(R) Ethernet Controller I225-V", EthernetMac, "192.168.1.20", 24, true),
        new("ZeroTier One [8056c2e21c000001]", "ZeroTier Virtual Port", "22:CD:1A:EE:A7:2E", "10.147.17.5", 24, true),
        new("vEthernet (Default Switch)", "Hyper-V Virtual Ethernet Adapter", "00:15:5D:32:BB:C6", "172.21.208.1", 20, true),
        new("Wi-Fi", "Intel(R) Wi-Fi 6 AX201 160MHz", "C4:BD:E5:2E:9A:28", "169.254.40.12", 16, false),
    ];

    public IReadOnlyList<AudioDevice> GetAudioDevices() =>
    [
        new(SpeakersId, "Speakers (Realtek(R) Audio)", AudioDirection.Output, true),
        new("{0.0.0.00000000}.{1f2e3d4c-5b6a-4978-8a9b-0c1d2e3f4a5b}", "VG240Y (NVIDIA High Definition Audio)", AudioDirection.Output, false),
        new("{0.0.0.00000000}.{9a8b7c6d-5e4f-4a3b-2c1d-0e9f8a7b6c5d}", "Speakers (Steam Streaming Speakers)", AudioDirection.Output, false),
        new("{0.0.1.00000000}.{2b3c4d5e-6f70-4182-93a4-b5c6d7e8f901}", "Microphone (Arctis 7 Chat)", AudioDirection.Input, true),
        new("{0.0.1.00000000}.{3c4d5e6f-7081-4293-a4b5-c6d7e8f90a12}", "Microphone (HD Pro Webcam C920)", AudioDirection.Input, false),
    ];

    public IReadOnlyList<ControllerDevice> GetControllers() =>
    [
        new("Controller (Xbox One For Windows)", "Controller (Xbox One For Windows)", "Gamepad"),
        new("Logitech G29 Driving Force Racing Wheel", "Logitech G29 Driving Force Racing Wheel", "Driving"),
    ];
}
#endif
