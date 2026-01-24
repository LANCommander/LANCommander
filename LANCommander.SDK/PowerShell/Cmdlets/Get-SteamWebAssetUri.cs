using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.Steam;
using LANCommander.Steam.Services;
using Svrooij.PowerShell.DI;

namespace LANCommander.SDK.PowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Get, "SteamWebAssetUri")]
[OutputType(typeof(Uri))]
[GenerateBindings]
public partial class GetSteamWebAssetUriCmdlet : DependencyCmdlet<PowerShellStartup>
{
    [Parameter(Mandatory = true, Position = 0)]
    public int AppId { get; set; }

    [Parameter(Mandatory = true, Position = 1)]
    public WebAssetType WebAssetType { get; set; }

    public override async Task ProcessRecordAsync(CancellationToken cancellationToken)
    {
        try
        {
            var uri = SteamStoreService.GetWebAssetUri(AppId, WebAssetType);
            WriteObject(uri);
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "GetWebAssetUriError", ErrorCategory.OperationStopped, null));
        }
    }
}
