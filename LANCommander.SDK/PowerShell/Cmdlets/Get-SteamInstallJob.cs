using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.Steam.Abstractions;
using LANCommander.Steam.Models;
using Svrooij.PowerShell.DI;

namespace LANCommander.SDK.PowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Get, "SteamInstallJob")]
[OutputType(typeof(SteamCmdInstallJob))]
[GenerateBindings]
public partial class GetSteamInstallJobCmdlet : DependencyCmdlet<PowerShellStartup>
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
            var job = _steamCmdService.GetInstallJob(JobId);
            if (job != null)
            {
                WriteObject(job);
            }
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "GetInstallJobError", ErrorCategory.OperationStopped, null));
        }
    }
}
