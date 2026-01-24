using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.Steam.Abstractions;
using LANCommander.Steam.Enums;
using Svrooij.PowerShell.DI;

namespace LANCommander.SDK.PowerShell.Cmdlets;

[Cmdlet(VerbsCommunications.Disconnect, "SteamCmd")]
[OutputType(typeof(SteamCmdStatus))]
[GenerateBindings]
public partial class DisconnectSteamCmdCmdlet : DependencyCmdlet<PowerShellStartup>
{
    [Parameter(Mandatory = true, Position = 0)]
    public string Username { get; set; } = string.Empty;

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
            var status = await _steamCmdService.LogoutAsync(Username);
            
            WriteObject(status);
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "LogoutError", ErrorCategory.OperationStopped, null));
        }
    }
}
