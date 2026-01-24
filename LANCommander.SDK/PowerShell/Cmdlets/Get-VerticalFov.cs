using System;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.PowerShell.Models;
using System.Linq;
using System.Management.Automation;

namespace LANCommander.SDK.PowerShell.Cmdlets
{
    [Cmdlet(VerbsCommon.Get, "VerticalFov")]
    [OutputType(typeof(string))]
    public class GetVerticalFovCmdlet : Cmdlet
    {
        [Parameter] public int Width { get; set; } = 0;
        [Parameter] public int Height { get; set; } = 0;
        [Parameter] public int BaseFov { get; set; } = 75;
        
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
