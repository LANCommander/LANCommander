using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.Steam.Abstractions;
using Svrooij.PowerShell.DI;

namespace LANCommander.SDK.PowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Get, "SteamCmdPath")]
[OutputType(typeof(string))]
[GenerateBindings]
public partial class GetSteamCmdPathCmdlet : DependencyCmdlet<PowerShellStartup>
{
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
            var path = await _steamCmdService.AutoDetectSteamCmdPathAsync();
            if (!string.IsNullOrEmpty(path))
            {
                WriteObject(path);
            }
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "AutoDetectPathError", ErrorCategory.OperationStopped, null));
        }
    }
}
