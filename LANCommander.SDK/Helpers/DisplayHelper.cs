using LANCommander.SDK.PowerShell.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LANCommander.SDK.Helpers
{
    public class DisplayHelper
    {
        public static DEVMODE GetDeviceMode()
        {
            DEVMODE devMode = new DEVMODE();
            devMode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref devMode);
            return devMode;
        }

        [DllImport("user32.dll")]
        private static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

        private const int ENUM_CURRENT_SETTINGS = -1;

        [StructLayout(LayoutKind.Sequential)]
        public struct DEVMODE
        {
            private const int CCHDEVICENAME = 32;
            private const int CCHFORMNAME = 32;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)]
            public string dmDeviceName;

            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;

            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;

            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHFORMNAME)]
            public string dmFormName;

            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmNup;

            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        public static Screen GetScreen()
        {
            var screen = new Screen();
            var bounds = new Bounds();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var devMode = GetDeviceMode();

                bounds.Width = devMode.dmPelsWidth;
                bounds.Height = devMode.dmPelsHeight;

                screen.RefreshRate = devMode.dmDisplayFrequency;
                screen.BitsPerPixel = devMode.dmBitsPerPel;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Try each source in order of reliability.
                // xrandr works on X11 and XWayland; xdpyinfo is a lighter X11 fallback;
                // /sys/class/drm works without a display server and covers Wayland-only setups.
                if (!TryGetScreenFromXrandr(out bounds, out var refreshRate, out var bitsPerPixel) &&
                    !TryGetScreenFromXdpyinfo(out bounds) &&
                    !TryGetScreenFromDrm(out bounds))
                {
                    // Nothing worked — leave bounds at zero so callers can detect the failure.
                }

                screen.RefreshRate  = refreshRate;
                screen.BitsPerPixel = bitsPerPixel;
            }

            screen.Primary = true;
            screen.Bounds  = bounds;
            screen.Width   = bounds.Width;
            screen.Height  = bounds.Height;

            return screen;
        }

        // ── Linux helpers ─────────────────────────────────────────────────────────

        /// <summary>
        /// Parses <c>xrandr</c> output to find the active resolution and refresh rate.
        /// Works on X11 and XWayland.
        ///
        /// Example xrandr output:
        /// <code>
        /// Screen 0: minimum 320 x 200, current 1920 x 1080, maximum 16384 x 16384
        /// DP-1 connected primary 1920x1080+0+0 ...
        ///    1920x1080     60.00*+  50.00   59.94
        ///    1280x720      60.00    59.94
        /// </code>
        /// </summary>
        private static bool TryGetScreenFromXrandr(out Bounds bounds, out int refreshRate, out int bitsPerPixel)
        {
            bounds       = new Bounds();
            refreshRate  = 0;
            bitsPerPixel = 24; // xrandr does not report bit depth; 24 is the near-universal default

            try
            {
                var output = RunProcess("xrandr", "");
                if (string.IsNullOrWhiteSpace(output))
                    return false;

                // "Screen 0: ... current 1920 x 1080 ..."
                var screenMatch = Regex.Match(output, @"current\s+(\d+)\s*x\s*(\d+)");
                if (!screenMatch.Success)
                    return false;

                bounds.Width  = int.Parse(screenMatch.Groups[1].Value);
                bounds.Height = int.Parse(screenMatch.Groups[2].Value);

                // A mode line looks like:  "   1920x1080     60.00*+  50.00  59.94"
                // The active refresh rate is the one immediately followed by '*'.
                var refreshMatch = Regex.Match(output, @"(\d+\.\d+)\*");
                if (refreshMatch.Success &&
                    float.TryParse(refreshMatch.Groups[1].Value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var rate))
                {
                    refreshRate = (int)Math.Round(rate);
                }

                return bounds.Width > 0 && bounds.Height > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Parses <c>xdpyinfo</c> output to find the screen dimensions.
        /// Lighter than xrandr; does not provide refresh rate.
        ///
        /// Example relevant line:
        /// <code>
        ///   dimensions:    1920x1080 pixels (508x286 millimeters)
        /// </code>
        /// </summary>
        private static bool TryGetScreenFromXdpyinfo(out Bounds bounds)
        {
            bounds = new Bounds();

            try
            {
                var output = RunProcess("xdpyinfo", "");
                if (string.IsNullOrWhiteSpace(output))
                    return false;

                var match = Regex.Match(output, @"dimensions:\s+(\d+)x(\d+)\s+pixels");
                if (!match.Success)
                    return false;

                bounds.Width  = int.Parse(match.Groups[1].Value);
                bounds.Height = int.Parse(match.Groups[2].Value);

                return bounds.Width > 0 && bounds.Height > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Reads the preferred/current mode from the kernel DRM subsystem.
        /// Works without any display server — covers pure Wayland and headless setups.
        ///
        /// Each connected output exposes its mode list at
        /// <c>/sys/class/drm/card*-*/modes</c>; the first line is the preferred mode.
        /// </summary>
        private static bool TryGetScreenFromDrm(out Bounds bounds)
        {
            bounds = new Bounds();

            try
            {
                var modeFiles = Directory.GetFiles("/sys/class/drm", "modes", SearchOption.AllDirectories);

                foreach (var file in modeFiles)
                {
                    var firstLine = File.ReadLines(file).FirstOrDefault();
                    if (string.IsNullOrWhiteSpace(firstLine))
                        continue;

                    // Format: "1920x1080"
                    var match = Regex.Match(firstLine.Trim(), @"^(\d+)x(\d+)$");
                    if (!match.Success)
                        continue;

                    bounds.Width  = int.Parse(match.Groups[1].Value);
                    bounds.Height = int.Parse(match.Groups[2].Value);

                    if (bounds.Width > 0 && bounds.Height > 0)
                        return true;
                }
            }
            catch
            {
                // /sys may not be accessible in all environments
            }

            return false;
        }

        private static string RunProcess(string fileName, string arguments)
        {
            var psi = new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            };

            using var process = Process.Start(psi);
            if (process == null)
                return string.Empty;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return process.ExitCode == 0 ? output : string.Empty;
        }
    }
}
