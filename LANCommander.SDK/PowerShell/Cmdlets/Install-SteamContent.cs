using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.Steam.Abstractions;
using LANCommander.Steam.Models;

namespace LANCommander.SDK.PowerShell.Cmdlets;

[Cmdlet(VerbsLifecycle.Install, "SteamContent")]
[OutputType(typeof(SteamCmdInstallJob))]
public class InstallSteamContentCmdlet : AsyncCmdlet
{
    [Parameter(Mandatory = true, Position = 0)]
    public uint AppId { get; set; }

    [Parameter(Mandatory = true, Position = 1)]
    public string InstallDirectory { get; set; } = string.Empty;

    [Parameter(Mandatory = false)]
    public string? Username { get; set; }

    protected override async Task ProcessRecordAsync(CancellationToken cancellationToken)
    {
        var steamCmdService = SteamServicesProvider.GetSteamCmdService(SessionState);

        try
        {
            await steamCmdService.InstallContentAsync(AppId, InstallDirectory, Username);
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "InstallContentError", ErrorCategory.OperationStopped, null));
        }
    }
}
