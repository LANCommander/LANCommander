using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.Steam.Abstractions;
using LANCommander.Steam.Models;
using Svrooij.PowerShell.DI;

namespace LANCommander.SDK.PowerShell.Cmdlets;

[Cmdlet(VerbsLifecycle.Install, "SteamContent")]
[OutputType(typeof(SteamCmdInstallJob))]
[GenerateBindings]
public partial class InstallSteamContentCmdlet : DependencyCmdlet<PowerShellStartup>
{
    [Parameter(Mandatory = true, Position = 0)]
    public uint AppId { get; set; }

    [Parameter(Mandatory = true, Position = 1)]
    public string InstallDirectory { get; set; } = string.Empty;

    [Parameter(Mandatory = false)]
    public string? Username { get; set; }

    [ServiceDependency]
    private LANCommander.Steam.Abstractions.ISteamCmdService _steamCmdService;

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
            var job = await _steamCmdService.InstallContentAsync(AppId, InstallDirectory, Username);
            WriteObject(job);
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "InstallContentError", ErrorCategory.OperationStopped, null));
        }
    }
}
