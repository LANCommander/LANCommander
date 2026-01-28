using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.Steam.Abstractions;
using LANCommander.Steam.Enums;

namespace LANCommander.SDK.PowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Remove, "SteamContent")]
[OutputType(typeof(SteamCmdStatus))]
public class RemoveSteamContentCmdlet : AsyncCmdlet
{
    [Parameter(Mandatory = true, Position = 0)]
    public string InstallDirectory { get; set; } = string.Empty;

    protected override async Task ProcessRecordAsync(CancellationToken cancellationToken)
    {
        var steamCmdService = SteamServicesProvider.GetSteamCmdService(SessionState);

        try
        {
            var status = await steamCmdService.RemoveContentAsync(InstallDirectory);
            WriteObject(status);
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "RemoveContentError", ErrorCategory.OperationStopped, null));
        }
    }
}
