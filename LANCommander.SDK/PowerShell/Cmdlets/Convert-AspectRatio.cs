using System;
using System.Management.Automation;

namespace LANCommander.SDK.PowerShell.Cmdlets
{
    public class DisplayResolution
    {
        public int Width { get; set; }
        public int Height { get; set; }
    }

    [Cmdlet(VerbsData.Convert, "AspectRatio")]
    [OutputType(typeof(string))]
    public class ConvertAspectRatioCmdlet : Cmdlet
    {
        [Parameter(Mandatory = true, Position = 0)]
        public int Width { get; set; }

        [Parameter(Mandatory = true, Position = 1)]
        public int Height { get; set; }

        [Parameter(Mandatory = true, Position = 2)]
        public double AspectRatio { get; set; }

        protected override void ProcessRecord()
        {
            var resolution = new DisplayResolution();
            
            // Display is wider, pillar box
            if ((Width / Height) < AspectRatio)
            {
                resolution.Width = (int)Math.Ceiling(Height * AspectRatio);
                resolution.Height = Height;
            }
            // Letterbox
            else
            {
                resolution.Width = Width;
                resolution.Height = (int)Math.Ceiling(Width * (1 / AspectRatio));
            }

            WriteObject(resolution);
        }
    }
}
