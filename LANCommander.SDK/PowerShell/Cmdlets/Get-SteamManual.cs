using System;
using System.IO;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.Steam.Services;
using Svrooij.PowerShell.DI;

namespace LANCommander.SDK.PowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Get, "SteamManual")]
[OutputType(typeof(byte[]))]
[GenerateBindings]
public partial class GetSteamManualCmdlet : DependencyCmdlet<PowerShellStartup>
{
    [Parameter(Mandatory = true, Position = 0)]
    public int AppId { get; set; }

    [Parameter(Mandatory = false)]
    public string? OutputPath { get; set; }

    [ServiceDependency]
    private LANCommander.Steam.Services.SteamStoreService _steamStoreService;

    public override async Task ProcessRecordAsync(CancellationToken cancellationToken)
    {
        if (_steamStoreService == null)
        {
            WriteError(new ErrorRecord(
                new InvalidOperationException("SteamStoreService is not available in the PowerShell session"),
                "SteamStoreServiceNotAvailable",
                ErrorCategory.InvalidOperation,
                null));
            return;
        }

        try
        {
            var manualData = await _steamStoreService.DownloadManualAsync(AppId);
            
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
