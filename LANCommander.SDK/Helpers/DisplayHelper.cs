using LANCommander.SDK.PowerShell.Models;
using System;
using System.Collections.Generic;
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
                var process = new System.Diagnostics.Process();

                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.FileName = "xrandr";

                process.Start();

                var output = process.StandardOutput.ReadToEnd();

                process.WaitForExit();

                var match = Regex.Match(output, @"^\s+(\d+)x(\d+)\s+(\d+\.?\d+)\*\+$");

                var w = match.Groups[1].Value;
                var h = match.Groups[2].Value;
                var r = match.Groups[3].Value;

                bounds.Width = int.Parse(w);
                bounds.Height = int.Parse(h);
                screen.RefreshRate = (int)float.Parse(r);
            }

            screen.Primary = true;
            screen.Bounds = bounds;
            screen.Width = bounds.Width;
            screen.Height = bounds.Height;

            return screen;
        }
    }
}
