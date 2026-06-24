using LANCommander.Server.Settings.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Net.Mime;

namespace LANCommander.Server.Endpoints;

public static class LogEndpoints
{
    public static void MapLogEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/Logs")
            .RequireAuthorization();

        group.MapGet("/Download/{fileName}", DownloadAsync);
    }

    private static IResult DownloadAsync(
        string fileName,
        [FromServices] IOptions<Settings.Settings> settings)
    {
        var fileProvider = settings.Value.Server.Logs.Providers
            .FirstOrDefault(p => p.Type == LoggingProviderType.File && p.Enabled);

        if (fileProvider == null)
            return TypedResults.NotFound("No file logging provider is configured.");

        // Sanitize the file name to prevent directory traversal
        fileName = Path.GetFileName(fileName);

        if (!fileName.StartsWith("log-") || !fileName.EndsWith(".txt"))
            return TypedResults.BadRequest("Invalid log file name.");

        var logFilePath = Path.Combine(fileProvider.ConnectionString, fileName);

        if (!File.Exists(logFilePath))
            return TypedResults.NotFound("Log file not found.");

        var stream = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        return TypedResults.File(stream, MediaTypeNames.Text.Plain, fileName);
    }
}
