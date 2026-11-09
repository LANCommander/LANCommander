using System;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK.Factories;
using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;

namespace LANCommander.SDK.Helpers;

public static class LauncherEventHelper
{
    public static async Task ReportAsync(ApiRequestFactory requests, ILogger logger, LauncherEventType type, Guid targetId)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await requests.Create().UseAuthenticationToken().UseVersioning()
                .UseRoute("/api/Events/Launcher")
                .UseCancellationToken(timeout.Token)
                .AddBody(new LauncherEventReport { Type = type, TargetId = targetId }).PostAsync();
        }
        catch (Exception ex)
        {
            // Reporting is best-effort, including when a launcher connects to an older server.
            logger?.LogWarning("Could not report launcher event {EventType} for {TargetId}; failure type {FailureType}",
                type, targetId, ex.GetType().Name);
        }
    }
}
