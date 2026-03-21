using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.Steam.Abstractions;

namespace LANCommander.SDK.PowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Remove, "SteamCmdProfile")]
public class RemoveSteamCmdProfileCmdlet : AsyncCmdlet
{
    [Parameter(Mandatory = true, Position = 0)]
    public string Username { get; set; } = string.Empty;

    protected override async Task ProcessRecordAsync(CancellationToken cancellationToken)
    {
        var steamCmdService = SteamServicesProvider.GetSteamCmdService(SessionState);

        try
        {
            await steamCmdService.DeleteProfileAsync(Username);
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "DeleteProfileError", ErrorCategory.OperationStopped, null));
        }
    }
}
