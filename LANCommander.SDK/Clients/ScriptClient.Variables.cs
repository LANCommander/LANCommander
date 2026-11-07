using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LANCommander.SDK.Factories;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.SDK.Services;

public partial class ScriptClient
{
    /// <summary>The server's global script variables, which scripts read as <c>$var:Name</c>.</summary>
    public async Task<IEnumerable<GlobalVariable>> GetGlobalVariablesAsync()
    {
        return await serviceProvider
            .GetRequiredService<ApiRequestFactory>()
            .Create()
            .UseAuthenticationToken()
            .UseVersioning()
            .UseRoute("/api/Scripts/Variables")
            .GetAsync<IEnumerable<GlobalVariable>>();
    }

    /// <summary>
    /// Replaces the local global variables file with the server's, so scripts run with them (offline
    /// too, with the values from the last sync). A failed request keeps the last good copy.
    /// </summary>
    public async Task SyncGlobalVariablesAsync()
    {
        var destination = GlobalVariableStore.DefaultPath;

        try
        {
            var variables = await GetGlobalVariablesAsync();

            if (variables == null)
            {
                logger?.LogWarning("The server returned no global script variables; keeping the local copy");
                return;
            }

            var list = variables.ToList();

            GlobalVariableStore.Save(list, destination);

            logger?.LogInformation("Synced {Count} global script variables to {Destination}", list.Count, destination);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Could not sync global script variables");
        }
    }
}
