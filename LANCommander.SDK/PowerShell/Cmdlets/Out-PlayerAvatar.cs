using System.Management.Automation;
using LANCommander.SDK.Services;

namespace LANCommander.SDK.PowerShell.Cmdlets
{
    [Cmdlet(VerbsData.Out, "PlayerAvatar")]
    [OutputType(typeof(string))]
    public class OutPlayerAvatarCmdlet(ProfileService profileService) : BaseCmdlet
    {
        protected override void ProcessRecord()
        {
            var result = profileService.GetAvatarAsync().GetAwaiter().GetResult();

            WriteObject(result, false);
        }
    }
}
