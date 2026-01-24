using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.Steam.Abstractions;
using Svrooij.PowerShell.DI;

namespace LANCommander.SDK.PowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Remove, "SteamCmdProfile")]
[GenerateBindings]
public partial class RemoveSteamCmdProfileCmdlet : DependencyCmdlet<PowerShellStartup>
{
    [Parameter(Mandatory = true, Position = 0)]
    public string Username { get; set; } = string.Empty;

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
            await _steamCmdService.DeleteProfileAsync(Username);
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "DeleteProfileError", ErrorCategory.OperationStopped, null));
        }
    }
}
