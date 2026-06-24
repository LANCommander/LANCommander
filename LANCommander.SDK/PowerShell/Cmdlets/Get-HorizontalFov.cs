using System;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.PowerShell.Models;
using System.Linq;
using System.Management.Automation;

namespace LANCommander.SDK.PowerShell.Cmdlets
{
    [Cmdlet(VerbsCommon.Get, "HorizontalFov")]
    [OutputType(typeof(int))]
    public class GetHorizontalFovCmdlet : Cmdlet
    {
        [Parameter(HelpMessage = "The display width in pixels. Defaults to the primary display width.")]
        public int Width { get; set; } = 0;

        [Parameter(HelpMessage = "The display height in pixels. Defaults to the primary display height.")]
        public int Height { get; set; } = 0;

        [Parameter(HelpMessage = "The base horizontal field of view in degrees at 4:3 aspect ratio. Defaults to 90.")]
        public int BaseFov { get; set; } = 90;
        
        protected override void ProcessRecord()
        {
            var screen = DisplayHelper.GetScreen();
            
            if (Width == 0)
                Width = screen.Width;
            
            if (Height == 0)
                Height = screen.Height;

            WriteObject((int)GetHorizontalFovFromResolution(Width, Height, BaseFov));
        }
        
        private double GetHorizontalFovFromResolution(int width, int height, double baseFov)
        {
            double baseAspectRatio = 4.0 / 3.0;
            double currentAspectRatio = (double)width / height;

            double baseHFovRadians = Math.PI * baseFov / 180.0;
            double scaledHFovRadians = 2 * Math.Atan(Math.Tan(baseHFovRadians / 2) * (currentAspectRatio / baseAspectRatio));
            double scaledHFov = scaledHFovRadians * 180.0 / Math.PI;

            return Math.Round(scaledHFov, 0);
        }
    }
}
