using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.Steam.Abstractions;

namespace LANCommander.SDK.PowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Get, "SteamCmdPath")]
[OutputType(typeof(string))]
public class GetSteamCmdPathCmdlet : AsyncCmdlet
{
    protected override async Task ProcessRecordAsync(CancellationToken cancellationToken)
    {
        var steamCmdService = SteamServicesProvider.GetSteamCmdService(SessionState);

        try
        {
            var path = await steamCmdService.AutoDetectSteamCmdPathAsync();
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
