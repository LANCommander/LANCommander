using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.Steam.Services;

namespace LANCommander.SDK.PowerShell.Cmdlets;

[Cmdlet(VerbsDiagnostic.Test, "SteamManual")]
[OutputType(typeof(bool))]
public class TestSteamManualCmdlet : AsyncCmdlet
{
    [Parameter(Mandatory = true, Position = 0)]
    public int AppId { get; set; }

    protected override async Task ProcessRecordAsync(CancellationToken cancellationToken)
    {
        var steamStoreService = SteamServicesProvider.GetSteamStoreService(SessionState);

        try
        {
            var exists = await steamStoreService.HasManualAsync(AppId);
            WriteObject(exists);
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "HasManualError", ErrorCategory.OperationStopped, null));
        }
    }
}
