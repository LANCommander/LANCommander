using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.Steam.Abstractions;
using LANCommander.Steam.Models;

namespace LANCommander.SDK.PowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Get, "SteamCmdProfiles")]
[OutputType(typeof(SteamCmdProfile))]
public class GetSteamCmdProfilesCmdlet : AsyncCmdlet
{
    protected override async Task ProcessRecordAsync(CancellationToken cancellationToken)
    {
        var steamCmdService = SteamServicesProvider.GetSteamCmdService(SessionState);

        try
        {
            var profiles = await steamCmdService.GetProfilesAsync();
            foreach (var profile in profiles)
            {
                WriteObject(profile);
            }
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "GetProfilesError", ErrorCategory.OperationStopped, null));
        }
    }
}
