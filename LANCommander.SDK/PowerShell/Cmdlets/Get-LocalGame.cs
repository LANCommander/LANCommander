using System;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK.Models;

namespace LANCommander.SDK.PowerShell.Cmdlets;

/// <summary>
/// Lists games from the local library of the client running the script, including whether and
/// where each one is installed. Only available where the host provides a local library (the launcher).
/// </summary>
[Cmdlet(VerbsCommon.Get, "LocalGame")]
[OutputType(typeof(LocalGame))]
public class GetLocalGameCmdlet : AsyncCmdlet
{
    [Parameter(Position = 0, ValueFromPipelineByPropertyName = true, HelpMessage = "Only return the games with these IDs.")]
    public Guid[] Id { get; set; }

    [Parameter(HelpMessage = "Only return games whose title matches this value. Supports wildcards.")]
    [SupportsWildcards]
    public string Title { get; set; }

    [Parameter(HelpMessage = "Only return games that are installed.")]
    public SwitchParameter Installed { get; set; }

    protected override async Task ProcessRecordAsync(CancellationToken cancellationToken)
    {
        try
        {
            var provider = ScriptServicesProvider.GetLocalLibraryProvider(SessionState);

            var games = (await provider.GetGamesAsync(cancellationToken)).AsEnumerable();

            if (Id is { Length: > 0 })
                games = games.Where(g => Id.Contains(g.Id));

            if (!string.IsNullOrEmpty(Title))
            {
                var pattern = new WildcardPattern(Title, WildcardOptions.IgnoreCase);

                games = games.Where(g => g.Title is not null && pattern.IsMatch(g.Title));
            }

            if (Installed)
                games = games.Where(g => g.Installed);

            WriteObject(games.ToList(), true);
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "GetLocalGameError", ErrorCategory.NotSpecified, null));
        }
    }
}
