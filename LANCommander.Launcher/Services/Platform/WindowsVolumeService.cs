using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace LANCommander.Launcher.Services.Platform;

/// <summary>
/// Reads/writes the default render device master volume on Windows via the Core Audio COM API
/// (IMMDeviceEnumerator -> IMMDevice -> IAudioEndpointVolume). Interop is hand-declared so no
/// external audio dependency is required and the type compiles under both TFMs; it is only
/// instantiated on Windows.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsVolumeService : IVolumeService
{
    private static readonly Guid EventContext = Guid.NewGuid();

    public bool IsSupported => true;

    public int GetVolume()
    {
        return WithEndpointVolume(volume =>
        {
            volume.GetMasterVolumeLevelScalar(out var level);
            return Math.Clamp((int)Math.Round(level * 100), 0, 100);
        }, fallback: 0);
    }

    public void SetVolume(int percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        WithEndpointVolume(volume =>
        {
            var context = EventContext;
            volume.SetMasterVolumeLevelScalar(percent / 100f, ref context);
            return 0;
        }, fallback: 0);
    }

    public bool GetMuted()
    {
        return WithEndpointVolume(volume =>
        {
            volume.GetMute(out var muted);
            return muted;
        }, fallback: false);
    }

    private static T WithEndpointVolume<T>(Func<IAudioEndpointVolume, T> action, T fallback)
    {
        IMMDeviceEnumerator? enumerator = null;
        IMMDevice? device = null;
        IAudioEndpointVolume? endpointVolume = null;

        try
        {
            enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(
                Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"))!)!;

            if (enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out device) != 0 || device is null)
                return fallback;

            var iid = typeof(IAudioEndpointVolume).GUID;
            if (device.Activate(ref iid, ClsCtxAll, IntPtr.Zero, out var instance) != 0 || instance is not IAudioEndpointVolume volume)
                return fallback;

            endpointVolume = volume;
            return action(volume);
        }
        catch
        {
            return fallback;
        }
        finally
        {
            if (endpointVolume is not null) Marshal.ReleaseComObject(endpointVolume);
            if (device is not null) Marshal.ReleaseComObject(device);
            if (enumerator is not null) Marshal.ReleaseComObject(enumerator);
        }
    }

    private const int ClsCtxAll = 0x17;

    private enum EDataFlow { Render = 0, Capture = 1, All = 2 }

    private enum ERole { Console = 0, Multimedia = 1, Communications = 2 }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(EDataFlow dataFlow, int stateMask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice? endpoint);
        // Remaining methods intentionally omitted; only vtable order up to here matters.
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object? instance);
        // Remaining methods intentionally omitted.
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int GetChannelCount(out int channelCount);
        [PreserveSig] int SetMasterVolumeLevel(float levelDb, ref Guid eventContext);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);
        [PreserveSig] int GetMasterVolumeLevel(out float levelDb);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint channel, float levelDb, ref Guid eventContext);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid eventContext);
        [PreserveSig] int GetChannelVolumeLevel(uint channel, out float levelDb);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid eventContext);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
        // Remaining methods intentionally omitted.
    }
}
