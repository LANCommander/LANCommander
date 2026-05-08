using System;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.PowerShell.Models;
using System.Linq;
using System.Management.Automation;

namespace LANCommander.SDK.PowerShell.Cmdlets
{
    [Cmdlet(VerbsCommon.Get, "VerticalFov")]
    [OutputType(typeof(int))]
    public class GetVerticalFovCmdlet : Cmdlet
    {
        [Parameter(HelpMessage = "The display width in pixels. Defaults to the primary display width.")]
        public int Width { get; set; } = 0;

        [Parameter(HelpMessage = "The display height in pixels. Defaults to the primary display height.")]
        public int Height { get; set; } = 0;

        [Parameter(HelpMessage = "The base vertical field of view in degrees at 4:3 aspect ratio. Defaults to 75.")]
        public int BaseFov { get; set; } = 75;
        
        protected override void ProcessRecord()
        {
            var screen = DisplayHelper.GetScreen();
            
            if (Width == 0)
                Width = screen.Width;
            
            if (Height == 0)
                Height = screen.Height;

            WriteObject((int)GetVerticalFovFromResolution(Width, Height, BaseFov));
        }
        
        private double GetVerticalFovFromResolution(int width, int height, int baseFov)
        {
            double baseAspectRatio = 4.0 / 3.0;
            double currentAspectRatio = (double)width / height;

            double verticalFovRadians = 2 * Math.Atan(Math.Tan((baseFov * Math.PI / 180.0) / 2) * (baseAspectRatio / currentAspectRatio));
            double verticalFov = verticalFovRadians * 180.0 / Math.PI;

            return Math.Round(verticalFov, 0);
        }
    }
}
