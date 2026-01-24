using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.Steam.Abstractions;
using Svrooij.PowerShell.DI;

namespace LANCommander.SDK.PowerShell.Cmdlets;

[Cmdlet(VerbsLifecycle.Stop, "SteamInstallJob")]
[OutputType(typeof(bool))]
[GenerateBindings]
public partial class StopSteamInstallJobCmdlet : DependencyCmdlet<PowerShellStartup>
{
    [Parameter(Mandatory = true, Position = 0)]
    public Guid JobId { get; set; }

    [ServiceDependency]
    private ISteamCmdService _steamCmdService;

    public override async Task ProcessRecordAsync(CancellationToken cancellationToken)
    {
        if (_steamCmdService == null)
        {
            WriteError(new ErrorRecord(
                new InvalidOperationException("SteamCmdService is not available in the PowerShell session"),
                "SteamCmdServiceNotAvailable",
                ErrorCategory.InvalidOperation,
                null));
            return;
        }

        try
        {
            var cancelled = await _steamCmdService.CancelInstallJobAsync(JobId);
            WriteObject(cancelled);
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "CancelInstallJobError", ErrorCategory.OperationStopped, null));
        }
    }
}
