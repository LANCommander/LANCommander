using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.Steam;
using LANCommander.Steam.Services;

namespace LANCommander.SDK.PowerShell.Cmdlets;

[Cmdlet(VerbsCommon.Search, "SteamGames")]
[OutputType(typeof(GameSearchResult))]
public class SearchSteamGamesCmdlet : AsyncCmdlet
{
    [Parameter(Mandatory = true, Position = 0)]
    public string Keyword { get; set; } = string.Empty;

    protected override async Task ProcessRecordAsync(CancellationToken cancellationToken)
    {
        var steamStoreService = SteamServicesProvider.GetSteamWebApiService(SessionState);

        try
        {
            var results = await steamStoreService.SearchGamesAsync(Keyword);
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
