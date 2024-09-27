using LANCommander.SDK.Extensions;
using LANCommander.SDK.Helpers;
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
            var screen = DisplayHelper.GetScreen();

            WriteObject(screen);
        }
    }
}
