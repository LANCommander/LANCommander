using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.Steam.Abstractions;
using LANCommander.Steam.Models;

namespace LANCommander.SDK.PowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Get, "SteamCmdProfile")]
[OutputType(typeof(SteamCmdProfile))]
public class GetSteamCmdProfileCmdlet : AsyncCmdlet
{
    [Parameter(Mandatory = true, Position = 0)]
    public string Username { get; set; } = string.Empty;

    protected override async Task ProcessRecordAsync(CancellationToken cancellationToken)
    {
        var steamCmdService = SteamServicesProvider.GetSteamCmdService(SessionState);

        try
        {
            var profile = await steamCmdService.GetProfileAsync(Username);
            if (profile != null)
            {
                WriteObject(profile);
            }
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "GetProfileError", ErrorCategory.OperationStopped, null));
        }
    }
}
