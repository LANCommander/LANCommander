using System.Linq;
using System.Management.Automation;
using System.Windows.Forms;

namespace LANCommander.PowerShell.Cmdlets
{
    [Cmdlet(VerbsCommon.Get, "PrimaryDisplay")]
    [OutputType(typeof(string))]
    public class GetPrimaryDisplayCmdlet : PSCmdlet
    {
        protected override void ProcessRecord()
        {
            var screens = Screen.AllScreens;

            WriteObject(screens.First(s => s.Primary));
        }
    }
}
