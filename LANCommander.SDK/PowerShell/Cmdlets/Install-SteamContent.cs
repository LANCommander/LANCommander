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
    [Parameter(Mandatory = true, Position = 0, HelpMessage = "The Steam application ID to install.")]
    public uint AppId { get; set; }

    [Parameter(Mandatory = true, Position = 1, HelpMessage = "The directory path where the content should be installed.")]
    public string InstallDirectory { get; set; } = string.Empty;

    [Parameter(Mandatory = false, HelpMessage = "The Steam account username to use for downloading. If omitted, the default or anonymous account is used.")]
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
