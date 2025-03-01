using LANCommander.SDK.Extensions;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.PowerShell.Models;
using System.Linq;
using System.Management.Automation;

namespace LANCommander.SDK.PowerShell.Cmdlets
{
    [Cmdlet(VerbsData.Out, "PlayerAvatar")]
    [OutputType(typeof(string))]
    public class OutPlayerAvatarCmdlet : BaseCmdlet
    {
        protected override void ProcessRecord()
        {
            var result = Client.Profile.GetAvatarAsync().GetAwaiter().GetResult();

            WriteObject(result, false);
        }
    }
}
