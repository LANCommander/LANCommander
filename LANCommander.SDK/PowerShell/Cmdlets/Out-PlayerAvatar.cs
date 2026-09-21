using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace LANCommander.SDK.PowerShell.Cmdlets;

/// <summary>
/// Writes the currently signed in user's avatar to the pipeline as raw bytes.
/// </summary>
[Cmdlet(VerbsData.Out, "PlayerAvatar")]
[OutputType(typeof(byte[]))]
public class OutPlayerAvatarCmdlet : AsyncCmdlet
{
    protected override async Task ProcessRecordAsync(CancellationToken cancellationToken)
    {
        try
        {
            var profileClient = ScriptServicesProvider.GetProfileClient(SessionState);

            var result = await profileClient.GetAvatarAsync();

            WriteObject(result, false);
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "OutPlayerAvatarError", ErrorCategory.NotSpecified, null));
        }
    }
}
