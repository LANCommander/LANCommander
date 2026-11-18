using System;
using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services;

/// <summary>
/// Draws the eye to the launcher from the taskbar, the way Discord does: an unread count badged
/// over its taskbar button, and the button flashing when a message arrives while the user is
/// elsewhere. Windows only for now (ITaskbarList3 overlays and FlashWindowEx); a no-op elsewhere.
/// </summary>
public sealed class TaskbarAttentionService(ILogger<TaskbarAttentionService> logger)
{
    ITaskbarList3? _taskbar;
    IntPtr _overlayIcon;
    int _badgeCount;

    /// <summary>Badges the main window's taskbar button with a count; zero clears it</summary>
    public void SetBadge(int count) => Dispatcher.UIThread.Post(() => ApplyBadge(Math.Max(0, count)));

    /// <summary>
    /// Flashes the window's taskbar button until the user brings it forward; the main window's
    /// when no window is given. Does nothing while the window already has focus.
    /// </summary>
    public void Flash(Window? window = null) => Dispatcher.UIThread.Post(() =>
    {
        if (!OperatingSystem.IsWindows())
            return;

        window ??= MainWindow;

        if (window == null || window.IsActive || Handle(window) is not { } hwnd)
            return;

        try
        {
            var info = new FLASHWINFO
            {
                cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
                hwnd = hwnd,
                dwFlags = FLASHW_TRAY | FLASHW_TIMERNOFG,
                uCount = 0,
                dwTimeout = 0,
            };

            FlashWindowEx(ref info);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not flash the taskbar button");
        }
    });

    void ApplyBadge(int count)
    {
        if (!OperatingSystem.IsWindows() || count == _badgeCount)
            return;

        if (MainWindow is not { } window || Handle(window) is not { } hwnd)
            return;

        try
        {
            _taskbar ??= CreateTaskbar();

            var icon = count > 0 ? CreateBadgeIcon(count, window.RenderScaling) : IntPtr.Zero;
            var description = count > 0 ? $"{count} unread {(count == 1 ? "message" : "messages")}" : null;

            _taskbar.SetOverlayIcon(hwnd, icon, description);

            // The taskbar keeps its own copy, so the last one can go
            if (_overlayIcon != IntPtr.Zero)
                DestroyIcon(_overlayIcon);

            _overlayIcon = icon;
            _badgeCount = count;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not badge the taskbar button");
        }
    }

    static Window? MainWindow =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    static IntPtr? Handle(Window window) =>
        window.TryGetPlatformHandle()?.Handle is { } handle && handle != IntPtr.Zero ? handle : null;

    static ITaskbarList3 CreateTaskbar()
    {
        var taskbar = (ITaskbarList3)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("56FDF344-FD6D-11d0-958A-006097C9A090"))!)!;

        taskbar.HrInit();

        return taskbar;
    }

    /// <summary>
    /// A filled circle with the count in it, as Discord draws it: the 16px overlay size scaled to the
    /// display, so it stays sharp. Red, like other apps' unread badges, to read against any taskbar.
    /// </summary>
    static IntPtr CreateBadgeIcon(int count, double scaling)
    {
        var (pixels, size) = RenderBadge(count, scaling);

        return CreateIcon(pixels, size);
    }

    /// <summary>The badge as premultiplied BGRA pixels, top row first</summary>
    internal static (byte[] Pixels, int Size) RenderBadge(int count, double scaling)
    {
        var size = (int)Math.Round(16 * Math.Max(1, scaling));
        var text = count > 99 ? "99+" : count.ToString(CultureInfo.InvariantCulture);

        var badge = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size / 2.0),
            Background = new SolidColorBrush(Color.FromRgb(0xE5, 0x48, 0x4D)),
            Child = new TextBlock
            {
                Text = text,
                Foreground = Brushes.White,
                FontWeight = FontWeight.Bold,
                FontSize = size * (text.Length switch { 1 => 0.72, 2 => 0.58, _ => 0.42 }),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };

        badge.Measure(new Size(size, size));
        badge.Arrange(new Rect(0, 0, size, size));

        using var bitmap = new RenderTargetBitmap(new PixelSize(size, size));

        bitmap.Render(badge);

        var stride = size * 4;
        var pixels = new byte[stride * size];
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);

        try
        {
            bitmap.CopyPixels(new PixelRect(0, 0, size, size), handle.AddrOfPinnedObject(), pixels.Length, stride);
        }
        finally
        {
            handle.Free();
        }

        return (pixels, size);
    }

    /// <summary>A 32-bit icon from premultiplied BGRA pixels, top row first; the caller destroys it</summary>
    internal static IntPtr CreateIcon(byte[] pixels, int size)
    {
        // Icons take straight alpha, where Avalonia renders premultiplied
        for (var i = 0; i < pixels.Length; i += 4)
        {
            var alpha = pixels[i + 3];

            if (alpha is 0 or 255)
                continue;

            pixels[i] = (byte)Math.Min(255, pixels[i] * 255 / alpha);
            pixels[i + 1] = (byte)Math.Min(255, pixels[i + 1] * 255 / alpha);
            pixels[i + 2] = (byte)Math.Min(255, pixels[i + 2] * 255 / alpha);
        }

        var header = new BITMAPV5HEADER
        {
            bV5Size = (uint)Marshal.SizeOf<BITMAPV5HEADER>(),
            bV5Width = size,
            bV5Height = -size, // top-down, as the pixels are
            bV5Planes = 1,
            bV5BitCount = 32,
            bV5Compression = BI_BITFIELDS,
            bV5RedMask = 0x00FF0000,
            bV5GreenMask = 0x0000FF00,
            bV5BlueMask = 0x000000FF,
            bV5AlphaMask = 0xFF000000,
        };

        var color = CreateDIBSection(IntPtr.Zero, ref header, DIB_RGB_COLORS, out var bits, IntPtr.Zero, 0);

        if (color == IntPtr.Zero)
            throw new InvalidOperationException("The badge bitmap could not be created");

        // The alpha channel carries the shape; the mask only has to exist
        var mask = CreateBitmap(size, size, 1, 1, IntPtr.Zero);

        try
        {
            Marshal.Copy(pixels, 0, bits, pixels.Length);

            var info = new ICONINFO { fIcon = true, hbmColor = color, hbmMask = mask };

            var icon = CreateIconIndirect(ref info);

            if (icon == IntPtr.Zero)
                throw new InvalidOperationException("The badge icon could not be created");

            return icon;
        }
        finally
        {
            DeleteObject(color);
            DeleteObject(mask);
        }
    }

    // ── Win32 ────────────────────────────────────────────────────────────────

    const uint FLASHW_TRAY = 0x2;
    const uint FLASHW_TIMERNOFG = 0xC;
    const uint BI_BITFIELDS = 3;
    const uint DIB_RGB_COLORS = 0;

    [StructLayout(LayoutKind.Sequential)]
    struct FLASHWINFO
    {
        public uint cbSize;
        public IntPtr hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ICONINFO
    {
        [MarshalAs(UnmanagedType.Bool)] public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct BITMAPV5HEADER
    {
        public uint bV5Size;
        public int bV5Width;
        public int bV5Height;
        public ushort bV5Planes;
        public ushort bV5BitCount;
        public uint bV5Compression;
        public uint bV5SizeImage;
        public int bV5XPelsPerMeter;
        public int bV5YPelsPerMeter;
        public uint bV5ClrUsed;
        public uint bV5ClrImportant;
        public uint bV5RedMask;
        public uint bV5GreenMask;
        public uint bV5BlueMask;
        public uint bV5AlphaMask;
        public uint bV5CSType;
        public int bV5Endpoints_1x, bV5Endpoints_1y, bV5Endpoints_1z;
        public int bV5Endpoints_2x, bV5Endpoints_2y, bV5Endpoints_2z;
        public int bV5Endpoints_3x, bV5Endpoints_3y, bV5Endpoints_3z;
        public uint bV5GammaRed;
        public uint bV5GammaGreen;
        public uint bV5GammaBlue;
        public uint bV5Intent;
        public uint bV5ProfileData;
        public uint bV5ProfileSize;
        public uint bV5Reserved;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool FlashWindowEx(ref FLASHWINFO info);

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr CreateIconIndirect(ref ICONINFO info);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyIcon(IntPtr icon);

    [DllImport("gdi32.dll")]
    static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPV5HEADER header, uint usage, out IntPtr bits, IntPtr section, uint offset);

    [DllImport("gdi32.dll")]
    static extern IntPtr CreateBitmap(int width, int height, uint planes, uint bitsPerPixel, IntPtr bits);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool DeleteObject(IntPtr handle);

    /// <summary>Declared in vtable order through to SetOverlayIcon, the last one used</summary>
    [ComImport]
    [Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ITaskbarList3
    {
        // ITaskbarList
        void HrInit();
        void AddTab(IntPtr hwnd);
        void DeleteTab(IntPtr hwnd);
        void ActivateTab(IntPtr hwnd);
        void SetActiveAlt(IntPtr hwnd);

        // ITaskbarList2
        void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fullscreen);

        // ITaskbarList3
        void SetProgressValue(IntPtr hwnd, ulong completed, ulong total);
        void SetProgressState(IntPtr hwnd, int flags);
        void RegisterTab(IntPtr tab, IntPtr mdi);
        void UnregisterTab(IntPtr tab);
        void SetTabOrder(IntPtr tab, IntPtr insertBefore);
        void SetTabActive(IntPtr tab, IntPtr mdi, uint reserved);
        void ThumbBarAddButtons(IntPtr hwnd, uint count, IntPtr buttons);
        void ThumbBarUpdateButtons(IntPtr hwnd, uint count, IntPtr buttons);
        void ThumbBarSetImageList(IntPtr hwnd, IntPtr imageList);
        void SetOverlayIcon(IntPtr hwnd, IntPtr icon, [MarshalAs(UnmanagedType.LPWStr)] string? description);
    }
}
