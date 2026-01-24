using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.Steam.Abstractions;
using LANCommander.Steam.Models;
using Svrooij.PowerShell.DI;

namespace LANCommander.SDK.PowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Get, "SteamCmdProfiles")]
[OutputType(typeof(SteamCmdProfile))]
[GenerateBindings]
public partial class GetSteamCmdProfilesCmdlet : DependencyCmdlet<PowerShellStartup>
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
            var profiles = await _steamCmdService.GetProfilesAsync();
            foreach (var profile in profiles)
            {
                WriteObject(profile);
            }
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "GetProfilesError", ErrorCategory.OperationStopped, null));
        }
    }
}
