using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.Steam;
using LANCommander.Steam.Services;
using Svrooij.PowerShell.DI;

namespace LANCommander.SDK.PowerShell.Cmdlets;

[Cmdlet(VerbsDiagnostic.Test, "SteamWebAsset")]
[OutputType(typeof(bool))]
[GenerateBindings]
public partial class TestSteamWebAssetCmdlet : DependencyCmdlet<PowerShellStartup>
{
    [Parameter(Mandatory = true, Position = 0)]
    public int AppId { get; set; }

    [Parameter(Mandatory = true, Position = 1)]
    public WebAssetType WebAssetType { get; set; }

    [ServiceDependency]
    private SteamStoreService _steamStoreService;

    public override async Task ProcessRecordAsync(CancellationToken cancellationToken)
    {
        if (_steamStoreService == null)
        {
            WriteError(new ErrorRecord(
                new InvalidOperationException("SteamStoreService is not available in the PowerShell session"),
                "SteamStoreServiceNotAvailable",
                ErrorCategory.InvalidOperation,
                null));
            return;
        }

        try
        {
            var (exists, _) = await _steamStoreService.HasWebAssetAsync(AppId, WebAssetType);
            WriteObject(exists);
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "HasWebAssetError", ErrorCategory.OperationStopped, null));
        }
    }
}
