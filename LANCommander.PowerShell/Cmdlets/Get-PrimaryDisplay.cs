using LANCommander.PowerShell.Extensions;
using LANCommander.PowerShell.Models;
using System.Linq;
using System.Management.Automation;

namespace LANCommander.PowerShell.Cmdlets
{
    [Cmdlet(VerbsCommon.Get, "PrimaryDisplay")]
    [OutputType(typeof(string))]
    public class GetPrimaryDisplayCmdlet : Cmdlet
    {
        protected override void ProcessRecord()
        {
            var screens = System.Windows.Forms.Screen.AllScreens;

            var primaryScreen = screens.First(s => s.Primary);

            var mode = primaryScreen.GetDeviceMode();

            var screen = new Screen
            {
                Bounds = new Bounds
                {
                    Width = primaryScreen.Bounds.Width,
                    Height = primaryScreen.Bounds.Height
                },
                Width = primaryScreen.Bounds.Width,
                Height = primaryScreen.Bounds.Height,
                Primary = true,
                RefreshRate = mode.dmNup,
                BitsPerPixel = primaryScreen.BitsPerPixel
            };

            WriteObject(screen);
        }
    }
}
