using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services.Platform;

/// <summary>
/// Lists this machine's network adapters, audio endpoints and DirectInput game controllers. Audio comes
/// from Core Audio and controllers from DirectInput 8 itself, so the names are the ones the Interposer
/// matches on inside the game. Interop is hand-declared, as in <see cref="WindowsVolumeService"/>.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsDeviceCatalog(ILogger<WindowsDeviceCatalog> logger) : IDeviceCatalog
{
    public IReadOnlyList<NetworkAdapterDevice> GetNetworkAdapters()
    {
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(adapter => adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .ToList();

            // .NET also lists each filter driver bound to an adapter -- "Ethernet-QoS Packet Scheduler-0000"
            // -- as an interface of its own. Games never see those, so neither does the player.
            var names = interfaces.Select(adapter => adapter.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

            bool IsFilterDriver(NetworkInterface adapter) =>
                FilterSuffix.IsMatch(adapter.Name)
                && names.Any(name => name.Length < adapter.Name.Length && adapter.Name.StartsWith(name + "-", StringComparison.OrdinalIgnoreCase));

            return interfaces
                .Where(adapter => !IsFilterDriver(adapter))
                .Select(adapter =>
                {
                    var ipv4 = adapter.GetIPProperties().UnicastAddresses
                        .FirstOrDefault(address => address.Address.AddressFamily == AddressFamily.InterNetwork);

                    var mac = adapter.GetPhysicalAddress().GetAddressBytes();

                    return new NetworkAdapterDevice(
                        adapter.Name,
                        adapter.Description,
                        mac.Length == 6 ? string.Join(':', mac.Select(b => b.ToString("X2"))) : null,
                        ipv4?.Address.ToString(),
                        ipv4?.PrefixLength,
                        adapter.OperationalStatus == OperationalStatus.Up);
                })
                // An adapter with neither an address nor a MAC -- a WAN miniport, a virtual switch's port --
                // gives the filter nothing to pick it by
                .Where(adapter => adapter.Subnet != null || adapter.MacAddress != null)
                .OrderByDescending(adapter => adapter.IsConnected)
                .ThenBy(adapter => adapter.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        catch (Exception ex) when (ex is NetworkInformationException or PlatformNotSupportedException)
        {
            logger.LogWarning(ex, "Could not list the network adapters");
            return Array.Empty<NetworkAdapterDevice>();
        }
    }

    private static readonly System.Text.RegularExpressions.Regex FilterSuffix = new(@"-\d{4}$");

    // ── Audio (Core Audio) ─────────────────────────────────────────────────────────────────────────

    public IReadOnlyList<AudioDevice> GetAudioDevices()
    {
        var devices = new List<AudioDevice>();
        IMMDeviceEnumerator? enumerator = null;

        try
        {
            enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(
                Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"))!)!;

            foreach (var direction in new[] { AudioDirection.Output, AudioDirection.Input })
            {
                var flow = direction == AudioDirection.Output ? EDataFlow.Render : EDataFlow.Capture;
                var defaultId = DefaultEndpointId(enumerator, flow);

                if (enumerator.EnumAudioEndpoints(flow, DeviceStateActive, out var collection) != 0 || collection == null)
                    continue;

                try
                {
                    collection.GetCount(out var count);

                    for (uint i = 0; i < count; i++)
                    {
                        if (collection.Item(i, out var device) != 0 || device == null)
                            continue;

                        try
                        {
                            device.GetId(out var id);
                            devices.Add(new AudioDevice(id, FriendlyName(device) ?? id, direction, id == defaultId));
                        }
                        finally
                        {
                            Marshal.ReleaseComObject(device);
                        }
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(collection);
                }
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        {
            logger.LogWarning(ex, "Could not list the audio devices");
        }
        finally
        {
            if (enumerator != null)
                Marshal.ReleaseComObject(enumerator);
        }

        return devices;
    }

    private static string? DefaultEndpointId(IMMDeviceEnumerator enumerator, EDataFlow flow)
    {
        if (enumerator.GetDefaultAudioEndpoint(flow, ERole.Console, out var device) != 0 || device == null)
            return null;

        try
        {
            device.GetId(out var id);
            return id;
        }
        finally
        {
            Marshal.ReleaseComObject(device);
        }
    }

    private static string? FriendlyName(IMMDevice device)
    {
        if (device.OpenPropertyStore(StgmRead, out var store) != 0 || store == null)
            return null;

        try
        {
            var key = PkeyDeviceFriendlyName;

            if (store.GetValue(ref key, out var value) != 0)
                return null;

            try
            {
                return value.ValueType == VtLpwstr ? Marshal.PtrToStringUni(value.Pointer) : null;
            }
            finally
            {
                PropVariantClear(ref value);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(store);
        }
    }

    private const int DeviceStateActive = 0x1;
    private const int StgmRead = 0;
    private const ushort VtLpwstr = 31;

    private static readonly PropertyKey PkeyDeviceFriendlyName = new(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 14);

    private enum EDataFlow { Render = 0, Capture = 1, All = 2 }

    private enum ERole { Console = 0, Multimedia = 1, Communications = 2 }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct PropertyKey(Guid formatId, int propertyId)
    {
        public readonly Guid FormatId = formatId;
        public readonly int PropertyId = propertyId;
    }

    /// <summary>Only the parts of a PROPVARIANT a string needs: its type and pointer. Sized for x64.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct PropVariant
    {
        [FieldOffset(0)] public ushort ValueType;
        [FieldOffset(8)] public IntPtr Pointer;
    }

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant value);

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(EDataFlow dataFlow, int stateMask, out IMMDeviceCollection? devices);
        [PreserveSig] int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice? endpoint);
        // Remaining methods intentionally omitted; only vtable order up to here matters.
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int Item(uint index, out IMMDevice? device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object? instance);
        [PreserveSig] int OpenPropertyStore(int access, out IPropertyStore? properties);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        // Remaining methods intentionally omitted.
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
        // Remaining methods intentionally omitted.
    }

    // ── Controllers (DirectInput 8) ─────────────────────────────────────────────────────────────────

    public IReadOnlyList<ControllerDevice> GetControllers()
    {
        var controllers = new List<ControllerDevice>();
        var iid = new Guid("BF798031-483A-4DA2-AA99-5D64ED369700"); // IID_IDirectInput8W

        try
        {
            if (DirectInput8Create(GetModuleHandleW(null), DirectInputVersion, ref iid, out var pointer, IntPtr.Zero) != 0 || pointer == IntPtr.Zero)
                return controllers;

            var directInput = (IDirectInput8W)Marshal.GetObjectForIUnknown(pointer);
            Marshal.Release(pointer);

            try
            {
                EnumDevicesCallback callback = (ref DeviceInstance instance, IntPtr _) =>
                {
                    controllers.Add(new ControllerDevice(instance.InstanceName, instance.ProductName, ClassName(instance.DeviceType)));
                    return EnumContinue;
                };

                // Passed as a function pointer: a delegate parameter on a COM interface is not marshalled
                directInput.EnumDevices(DeviceClassGameControllers, Marshal.GetFunctionPointerForDelegate(callback), IntPtr.Zero, AttachedOnly);
                GC.KeepAlive(callback);
            }
            finally
            {
                Marshal.ReleaseComObject(directInput);
            }
        }
        catch (Exception ex) when (ex is COMException or DllNotFoundException or EntryPointNotFoundException or InvalidCastException)
        {
            logger.LogWarning(ex, "Could not list the game controllers");
        }

        return controllers;
    }

    /// <summary>The filter's name for a DirectInput 8 device type (the low byte of dwDevType).</summary>
    internal static string ClassName(uint deviceType) => (deviceType & 0xFF) switch
    {
        0x12 => "Mouse",
        0x13 => "Keyboard",
        0x14 => "Joystick",
        0x15 => "Gamepad",
        0x16 => "Driving",
        0x17 => "Flight",
        0x18 => "FirstPerson",
        0x19 => "DeviceControl",
        0x1A => "ScreenPointer",
        0x1B => "Remote",
        0x1C => "Supplemental",
        _ => "Device",
    };

    private const uint DirectInputVersion = 0x0800;
    private const uint DeviceClassGameControllers = 4; // DI8DEVCLASS_GAMECTRL
    private const uint AttachedOnly = 1;               // DIEDFL_ATTACHEDONLY
    private const int EnumContinue = 1;                // DIENUM_CONTINUE

    [DllImport("dinput8.dll")]
    private static extern int DirectInput8Create(IntPtr instance, uint version, ref Guid iid, out IntPtr directInput, IntPtr outer);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string? moduleName);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DeviceInstance
    {
        public uint Size;
        public Guid InstanceGuid;
        public Guid ProductGuid;
        public uint DeviceType;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string InstanceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string ProductName;
        public Guid ForceFeedbackDriverGuid;
        public ushort UsagePage;
        public ushort Usage;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int EnumDevicesCallback(ref DeviceInstance instance, IntPtr context);

    [ComImport, Guid("BF798031-483A-4DA2-AA99-5D64ED369700"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDirectInput8W
    {
        [PreserveSig] int CreateDevice(ref Guid instance, out IntPtr device, IntPtr outer);
        [PreserveSig] int EnumDevices(uint deviceClass, IntPtr callback, IntPtr context, uint flags);
        // Remaining methods intentionally omitted.
    }
}
