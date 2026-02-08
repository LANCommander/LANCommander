using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.Steam.Models.SteamCmdNet;

namespace LANCommander.SDK.PowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Get, "SteamAppInfo")]
[OutputType(typeof(AppInfo))]
public class GetSteamAppInfoCmdlet : AsyncCmdlet
{
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    [ValidateRange(1u, uint.MaxValue)]
    public uint AppId { get; set; }

    protected override async Task ProcessRecordAsync(CancellationToken cancellationToken)
    {
        var steamStoreService = SteamServicesProvider.GetSteamWebApiService(SessionState);

        try
        {
            var info = await steamStoreService.GetAppInfo(AppId);
            
            if (info == null)
                return;

            WriteObject(info);
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "SteamAppInfoError", ErrorCategory.OperationStopped, AppId));
        }
    }
}
