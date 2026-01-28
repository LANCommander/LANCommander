using System;
using System.IO;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.Steam.Services;

namespace LANCommander.SDK.PowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Get, "SteamManual")]
[OutputType(typeof(byte[]))]
public class GetSteamManualCmdlet : AsyncCmdlet
{
    [Parameter(Mandatory = true, Position = 0)]
    public int AppId { get; set; }

    [Parameter(Mandatory = false)]
    public string? OutputPath { get; set; }

    protected override async Task ProcessRecordAsync(CancellationToken cancellationToken)
    {
        var steamStoreService = SteamServicesProvider.GetSteamStoreService(SessionState);

        try
        {
            var manualData = await steamStoreService.DownloadManualAsync(AppId);
            
            if (manualData == null || manualData.Length == 0)
            {
                WriteWarning($"No manual found for App ID {AppId}");
                return;
            }

            if (!string.IsNullOrEmpty(OutputPath))
            {
                await File.WriteAllBytesAsync(OutputPath, manualData, cancellationToken);
                WriteObject(OutputPath);
            }
            else
            {
                WriteObject(manualData);
            }
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "DownloadManualError", ErrorCategory.OperationStopped, null));
        }
    }
}
