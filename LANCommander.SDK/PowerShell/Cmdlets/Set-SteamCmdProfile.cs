using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.Steam.Abstractions;
using LANCommander.Steam.Models;

namespace LANCommander.SDK.PowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Set, "SteamCmdProfile")]
public class SetSteamCmdProfileCmdlet : AsyncCmdlet
{
    [Parameter(Mandatory = true, Position = 0)]
    public string Username { get; set; } = string.Empty;

    [Parameter(Mandatory = true, Position = 1)]
    public string InstallDirectory { get; set; } = string.Empty;

    protected override async Task ProcessRecordAsync(CancellationToken cancellationToken)
    {
        var steamCmdService = SteamServicesProvider.GetSteamCmdService(SessionState);

        try
        {
            var profile = new SteamCmdProfile
            {
                Username = Username,
                InstallDirectory = InstallDirectory
            };

            await steamCmdService.SaveProfileAsync(profile);
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "SaveProfileError", ErrorCategory.OperationStopped, null));
        }
    }
}
