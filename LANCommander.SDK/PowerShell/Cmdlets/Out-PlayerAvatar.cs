using System.Management.Automation;
using LANCommander.SDK.Services;

namespace LANCommander.SDK.PowerShell.Cmdlets
{
    [Cmdlet(VerbsData.Out, "PlayerAvatar")]
    [OutputType(typeof(string))]
    public class OutPlayerAvatarCmdlet(ProfileClient profileClient) : BaseCmdlet
    {
        protected override void ProcessRecord()
        {
            var result = profileClient.GetAvatarAsync().GetAwaiter().GetResult();

            WriteObject(result, false);
        }
    }
}
