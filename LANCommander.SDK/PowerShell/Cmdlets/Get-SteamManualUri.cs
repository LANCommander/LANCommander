using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.Steam.Services;

namespace LANCommander.SDK.PowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Get, "SteamManualUri")]
[OutputType(typeof(Uri))]
public class GetSteamManualUriCmdlet : AsyncCmdlet
{
    [Parameter(Mandatory = true, Position = 0)]
    public int AppId { get; set; }

    protected override async Task ProcessRecordAsync(CancellationToken cancellationToken)
    {
        try
        {
            var uri = SteamStoreService.GetManualUri(AppId);
            WriteObject(uri);
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "GetManualUriError", ErrorCategory.OperationStopped, null));
        }
    }
}
