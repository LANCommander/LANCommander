using System;
using System.IO;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK.Factories;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace LANCommander.SDK.PowerShell.Cmdlets;

/// <summary>
/// Extracts the latest archive for a game, redistributable, or tool from the LANCommander server.
/// When run in a server-side packaging context, reads directly from the local archive file.
/// When run in a client-side context, downloads the archive via the API.
/// </summary>
[Cmdlet("Expand", "LatestArchive")]
[OutputType(typeof(DirectoryInfo))]
public class ExpandLatestArchiveCmdlet : AsyncCmdlet
{
    private const string ApiRequestFactoryKey = "LANCommander.SDK.ApiRequestFactory";

    [Parameter(Mandatory = false, Position = 0, HelpMessage = "The destination directory to extract the archive contents into. Defaults to the current working directory.")]
    [Alias("Destination", "OutputPath")]
    public string DestinationPath { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The game ID to download. If not specified, uses the ID from the $GameManifest or $Game variable.")]
    public Guid? GameId { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The redistributable ID to download. If not specified, uses the ID from the $Redistributable variable.")]
    public Guid? RedistributableId { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The tool ID to download. If not specified, uses the ID from the $Tool variable.")]
    public Guid? ToolId { get; set; }

    protected override async Task ProcessRecordAsync(CancellationToken cancellationToken)
    {
        var destinationPath = string.IsNullOrEmpty(DestinationPath)
            ? SessionState.Path.CurrentFileSystemLocation.Path
            : GetUnresolvedProviderPathFromPSPath(DestinationPath);

        if (!Directory.Exists(destinationPath))
            Directory.CreateDirectory(destinationPath);

        var progressRecord = new ProgressRecord(0, "Expand-LatestArchive", "Resolving archive...");
        WriteProgress(progressRecord);

        // Check if we have a local archive path (server-side packaging context)
        var localArchivePath = SessionState.PSVariable.GetValue("LatestArchivePath") as string;

        if (!string.IsNullOrEmpty(localArchivePath) && File.Exists(localArchivePath))
        {
            await ExtractFromLocalFileAsync(localArchivePath, destinationPath, progressRecord, cancellationToken);
        }
        else
        {
            await DownloadAndExtractAsync(destinationPath, progressRecord, cancellationToken);
        }
    }

    private async Task ExtractFromLocalFileAsync(string archivePath, string destinationPath, ProgressRecord progressRecord, CancellationToken cancellationToken)
    {
        try
        {
            progressRecord.StatusDescription = $"Extracting {Path.GetFileName(archivePath)}...";
            progressRecord.PercentComplete = -1;
            WriteProgress(progressRecord);

            await using var fileStream = File.OpenRead(archivePath);
            await using var reader = await ReaderFactory.OpenAsyncReader(fileStream, new ReaderOptions(), cancellationToken);
            await reader.WriteAllToDirectoryAsync(destinationPath, new ExtractionOptions
            {
                ExtractFullPath = true,
                Overwrite = true
            }, cancellationToken);

            progressRecord.RecordType = ProgressRecordType.Completed;
            WriteProgress(progressRecord);

            WriteObject(new DirectoryInfo(destinationPath));
        }
        catch (OperationCanceledException)
        {
            WriteWarning("Extraction was canceled.");
            throw;
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "ExtractArchiveError", ErrorCategory.NotSpecified, archivePath));
        }
    }

    private async Task DownloadAndExtractAsync(string destinationPath, ProgressRecord progressRecord, CancellationToken cancellationToken)
    {
        var (entityId, route, entityLabel) = ResolveDownloadTarget();

        if (entityId == Guid.Empty)
        {
            WriteError(new ErrorRecord(
                new InvalidOperationException("Could not determine target ID. Ensure a context variable ($GameManifest, $Game, $Redistributable, or $Tool) is available, or specify -GameId, -RedistributableId, or -ToolId."),
                "NoTargetId", ErrorCategory.InvalidArgument, null));
            return;
        }

        var apiRequestFactory = SessionState.PSVariable.GetValue(ApiRequestFactoryKey) as ApiRequestFactory;

        if (apiRequestFactory == null)
        {
            WriteError(new ErrorRecord(
                new InvalidOperationException("ApiRequestFactory not available in session state. This cmdlet must be run within a LANCommander script context."),
                "NoApiRequestFactory", ErrorCategory.ResourceUnavailable, null));
            return;
        }

        try
        {
            progressRecord.StatusDescription = $"Downloading latest archive for {entityLabel} {entityId}...";
            progressRecord.PercentComplete = -1;
            WriteProgress(progressRecord);

            using var stream = await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute(route)
                .StreamAsync();

            progressRecord.StatusDescription = "Extracting archive...";
            WriteProgress(progressRecord);

            await using var reader = await ReaderFactory.OpenAsyncReader(stream, new ReaderOptions(), cancellationToken);
            await reader.WriteAllToDirectoryAsync(destinationPath, new ExtractionOptions
            {
                ExtractFullPath = true,
                Overwrite = true
            }, cancellationToken);

            progressRecord.RecordType = ProgressRecordType.Completed;
            WriteProgress(progressRecord);

            WriteObject(new DirectoryInfo(destinationPath));
        }
        catch (OperationCanceledException)
        {
            WriteWarning("Download was canceled.");
            throw;
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "ExpandLatestArchiveError", ErrorCategory.NotSpecified, entityId));
        }
    }

    private (Guid id, string route, string label) ResolveDownloadTarget()
    {
        // Explicit parameters take priority
        if (RedistributableId.HasValue && RedistributableId.Value != Guid.Empty)
            return (RedistributableId.Value, $"/api/Redistributables/{RedistributableId.Value}/Download", "redistributable");

        if (ToolId.HasValue && ToolId.Value != Guid.Empty)
            return (ToolId.Value, $"/api/Tools/{ToolId.Value}/Download", "tool");

        if (GameId.HasValue && GameId.Value != Guid.Empty)
            return (GameId.Value, $"/api/Games/{GameId.Value}/Download", "game");

        // Context variable resolution - check redistributable and tool first (more specific),
        // then fall back to game context
        var redistributable = SessionState.PSVariable.GetValue("Redistributable") as SDK.Models.Redistributable;
        if (redistributable != null && redistributable.Id != Guid.Empty)
            return (redistributable.Id, $"/api/Redistributables/{redistributable.Id}/Download", "redistributable");

        var tool = SessionState.PSVariable.GetValue("Tool") as SDK.Models.Tool;
        if (tool != null && tool.Id != Guid.Empty)
            return (tool.Id, $"/api/Tools/{tool.Id}/Download", "tool");

        // Client-side scripts use $GameManifest
        var manifest = SessionState.PSVariable.GetValue("GameManifest") as SDK.Models.Manifest.Game;
        if (manifest != null && manifest.Id != Guid.Empty)
            return (manifest.Id, $"/api/Games/{manifest.Id}/Download", "game");

        // Server-side package scripts use $Game
        var game = SessionState.PSVariable.GetValue("Game") as SDK.Models.Game;
        if (game != null && game.Id != Guid.Empty)
            return (game.Id, $"/api/Games/{game.Id}/Download", "game");

        return (Guid.Empty, null, null);
    }
}
