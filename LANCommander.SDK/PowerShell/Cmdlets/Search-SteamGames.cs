using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.Steam;
using LANCommander.Steam.Services;
using Svrooij.PowerShell.DI;

namespace LANCommander.SDK.PowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Search, "SteamGames")]
[OutputType(typeof(GameSearchResult))]
[GenerateBindings]
public partial class SearchSteamGamesCmdlet : DependencyCmdlet<PowerShellStartup>
{
    [Parameter(Mandatory = true, Position = 0)]
    public string Keyword { get; set; } = string.Empty;

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
            var results = await _steamStoreService.SearchGamesAsync(Keyword);
            foreach (var result in results)
            {
                WriteObject(result);
            }
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "SearchGamesError", ErrorCategory.OperationStopped, null));
        }
    }
}
