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
/// Extracts the latest archive for the current game from the LANCommander server.
/// When run in a server-side packaging context, reads directly from the local archive file.
/// When run in a client-side context, downloads the archive via the API.
/// </summary>
[Cmdlet(VerbsLifecycle.Install, "LatestArchive")]
[OutputType(typeof(DirectoryInfo))]
public class InstallLatestArchiveCmdlet : AsyncCmdlet
{
    private const string ApiRequestFactoryKey = "LANCommander.SDK.ApiRequestFactory";

    [Parameter(Mandatory = false, Position = 0, HelpMessage = "The destination directory to extract the archive contents into. Defaults to the current working directory.")]
    [Alias("Destination", "OutputPath")]
    public string DestinationPath { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The game ID to download. If not specified, uses the ID from the $GameManifest or $Game variable.")]
    public Guid? GameId { get; set; }

    protected override async Task ProcessRecordAsync(CancellationToken cancellationToken)
    {
        var destinationPath = string.IsNullOrEmpty(DestinationPath)
            ? SessionState.Path.CurrentFileSystemLocation.Path
            : GetUnresolvedProviderPathFromPSPath(DestinationPath);

        if (!Directory.Exists(destinationPath))
            Directory.CreateDirectory(destinationPath);

        var progressRecord = new ProgressRecord(0, "Install-LatestArchive", "Resolving archive...");
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
        var gameId = ResolveGameId();

        if (gameId == Guid.Empty)
        {
            WriteError(new ErrorRecord(
                new InvalidOperationException("Could not determine game ID. Ensure $GameManifest or $Game is available, or specify -GameId."),
                "NoGameId", ErrorCategory.InvalidArgument, null));
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
            progressRecord.StatusDescription = $"Downloading latest archive for game {gameId}...";
            progressRecord.PercentComplete = -1;
            WriteProgress(progressRecord);

            using var stream = await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute($"/api/Games/{gameId}/Download")
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
            WriteError(new ErrorRecord(ex, "InstallLatestArchiveError", ErrorCategory.NotSpecified, gameId));
        }
    }

    private Guid ResolveGameId()
    {
        if (GameId.HasValue && GameId.Value != Guid.Empty)
            return GameId.Value;

        // Client-side scripts use $GameManifest
        var manifest = SessionState.PSVariable.GetValue("GameManifest") as SDK.Models.Manifest.Game;
        if (manifest != null && manifest.Id != Guid.Empty)
            return manifest.Id;

        // Server-side package scripts use $Game
        var game = SessionState.PSVariable.GetValue("Game") as SDK.Models.Game;
        if (game != null && game.Id != Guid.Empty)
            return game.Id;

        return Guid.Empty;
    }
}
