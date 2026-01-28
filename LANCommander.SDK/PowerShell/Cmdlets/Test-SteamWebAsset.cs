using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.Steam;
using LANCommander.Steam.Services;

namespace LANCommander.SDK.PowerShell.Cmdlets;

[Cmdlet(VerbsDiagnostic.Test, "SteamWebAsset")]
[OutputType(typeof(bool))]
public class TestSteamWebAssetCmdlet : AsyncCmdlet
{
    [Parameter(Mandatory = true, Position = 0)]
    public int AppId { get; set; }

    [Parameter(Mandatory = true, Position = 1)]
    public WebAssetType WebAssetType { get; set; }

    protected override async Task ProcessRecordAsync(CancellationToken cancellationToken)
    {
        var steamStoreService = SteamServicesProvider.GetSteamStoreService(SessionState);

        try
        {
            var (exists, _) = await steamStoreService.HasWebAssetAsync(AppId, WebAssetType);
            WriteObject(exists);
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "HasWebAssetError", ErrorCategory.OperationStopped, null));
        }
    }
}
