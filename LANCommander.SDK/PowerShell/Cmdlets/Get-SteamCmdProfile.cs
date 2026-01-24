using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.Steam.Abstractions;
using LANCommander.Steam.Models;
using Svrooij.PowerShell.DI;

namespace LANCommander.SDK.PowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Get, "SteamCmdProfile")]
[OutputType(typeof(SteamCmdProfile))]
[GenerateBindings]
public partial class GetSteamCmdProfileCmdlet : DependencyCmdlet<PowerShellStartup>
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
            var profile = await _steamCmdService.GetProfileAsync(Username);
            if (profile != null)
            {
                WriteObject(profile);
            }
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "GetProfileError", ErrorCategory.OperationStopped, null));
        }
    }
}
