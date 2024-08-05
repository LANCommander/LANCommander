using LANCommander.SDK.Extensions;
using LANCommander.SDK.PowerShell.Models;
using System.Linq;
using System.Management.Automation;

namespace LANCommander.SDK.PowerShell.Cmdlets
{
    [Cmdlet(VerbsCommon.Get, "PrimaryDisplay")]
    [OutputType(typeof(string))]
    public class GetPrimaryDisplayCmdlet : Cmdlet
    {
        protected override void ProcessRecord()
        {
            var bounds = GetBounds();

            var screen = new Screen
            {
                Bounds = bounds,
                Width = bounds.Width,
                Height = bounds.Height,
                Primary = true,
                RefreshRate = 60,
                BitsPerPixel = 32
            };

            WriteObject(screen);
        }

        private Bounds GetBounds()
        {
            Bounds bounds = new Bounds();

            bounds.Width = 1920;
            bounds.Height = 1080;

            return bounds;
        }
    }
}
