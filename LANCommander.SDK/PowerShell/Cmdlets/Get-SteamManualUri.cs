using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.Steam.Services;
using Svrooij.PowerShell.DI;

namespace LANCommander.SDK.PowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Get, "SteamManualUri")]
[OutputType(typeof(Uri))]
[GenerateBindings]
public partial class GetSteamManualUriCmdlet : DependencyCmdlet<PowerShellStartup>
{
    [Parameter(Mandatory = true, Position = 0)]
    public int AppId { get; set; }

    public override async Task ProcessRecordAsync(CancellationToken cancellationToken)
    {
        try
        {
            var uri = SteamStoreService.GetManualUri(AppId);
            WriteObject(uri);
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "GetManualUriError", ErrorCategory.OperationStopped, null));
        }
    }
}
