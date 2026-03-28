using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Avalonia.Services;

public class TaskbarProgressService
{
    private readonly ILogger<TaskbarProgressService> _logger;
    private ITaskbarList3? _taskbarList;
    private IntPtr _hwnd;

    public TaskbarProgressService(ILogger<TaskbarProgressService> logger)
    {
        _logger = logger;
    }

    public void Initialize(IntPtr hwnd)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        _hwnd = hwnd;

        try
        {
            _taskbarList = (ITaskbarList3)new TaskbarListInstance();
            _taskbarList.HrInit();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize ITaskbarList3");
            _taskbarList = null;
        }
    }

    public void SetProgress(float progress)
    {
        if (_taskbarList == null || _hwnd == IntPtr.Zero) return;

        try
        {
            const ulong total = 100_000;
            var completed = (ulong)(progress * total);
            _taskbarList.SetProgressState(_hwnd, TBPFLAG.TBPF_NORMAL);
            _taskbarList.SetProgressValue(_hwnd, completed, total);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set taskbar progress");
        }
    }

    public void SetIndeterminate()
    {
        if (_taskbarList == null || _hwnd == IntPtr.Zero) return;

        try
        {
            _taskbarList.SetProgressState(_hwnd, TBPFLAG.TBPF_INDETERMINATE);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set indeterminate taskbar progress");
        }
    }

    public void ClearProgress()
    {
        if (_taskbarList == null || _hwnd == IntPtr.Zero) return;

        try
        {
            _taskbarList.SetProgressState(_hwnd, TBPFLAG.TBPF_NOPROGRESS);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear taskbar progress");
        }
    }

    // ── COM interop ──────────────────────────────────────────────────────────

    [Flags]
    private enum TBPFLAG
    {
        TBPF_NOPROGRESS    = 0x00,
        TBPF_INDETERMINATE = 0x01,
        TBPF_NORMAL        = 0x02,
        TBPF_ERROR         = 0x04,
        TBPF_PAUSED        = 0x08,
    }

    [ComImport]
    [Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITaskbarList3
    {
        void HrInit();
        void AddTab(IntPtr hwnd);
        void DeleteTab(IntPtr hwnd);
        void ActivateTab(IntPtr hwnd);
        void SetActiveAlt(IntPtr hwnd);
        void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fullscreen);
        void SetProgressValue(IntPtr hwnd, ulong completed, ulong total);
        void SetProgressState(IntPtr hwnd, TBPFLAG state);
    }

    [ComImport]
    [Guid("56fdf344-fd6d-11d0-958a-006097c9a090")]
    [ClassInterface(ClassInterfaceType.None)]
    private class TaskbarListInstance { }
}
