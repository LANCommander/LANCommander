using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.Steam;
using LANCommander.Steam.Services;

namespace LANCommander.SDK.PowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Get, "SteamWebAssetUri")]
[OutputType(typeof(Uri))]
public class GetSteamWebAssetUriCmdlet : AsyncCmdlet
{
    [Parameter(Mandatory = true, Position = 0)]
    public int AppId { get; set; }

    [Parameter(Mandatory = true, Position = 1)]
    public WebAssetType WebAssetType { get; set; }

    protected override async Task ProcessRecordAsync(CancellationToken cancellationToken)
    {
        try
        {
            var uri = SteamWebApiService.GetWebAssetUri(AppId, WebAssetType);
            WriteObject(uri);
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "GetWebAssetUriError", ErrorCategory.OperationStopped, null));
        }
    }
}
