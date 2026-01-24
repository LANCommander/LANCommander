using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.Steam.Services;
using Svrooij.PowerShell.DI;

namespace LANCommander.SDK.PowerShell.Cmdlets;

[Cmdlet(VerbsDiagnostic.Test, "SteamManual")]
[OutputType(typeof(bool))]
[GenerateBindings]
public partial class TestSteamManualCmdlet : DependencyCmdlet<PowerShellStartup>
{
    [Parameter(Mandatory = true, Position = 0)]
    public int AppId { get; set; }

    [ServiceDependency]
    private LANCommander.Steam.Services.SteamStoreService _steamStoreService;

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
            var exists = await _steamStoreService.HasManualAsync(AppId);
            WriteObject(exists);
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "HasManualError", ErrorCategory.OperationStopped, null));
        }
    }
}
