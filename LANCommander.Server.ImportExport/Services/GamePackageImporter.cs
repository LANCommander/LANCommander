using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;
using LANCommander.Server.Plugins;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.ImportExport.Services;

/// <summary>Imports plugin-provided LCX streams without routing them through the HTTP upload API.</summary>
public sealed class GamePackageImporter(
    ImportRunner importRunner,
    ILogger<GamePackageImporter> logger) : IGamePackageImporter
{
    private const int BufferSize = 1024 * 1024;

    public async Task<ImportResponse> ImportAsync(
        Stream package,
        GamePackageImportOptions? options = null,
        IProgress<GamePackageImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);

        if (!package.CanRead)
            throw new ArgumentException("The package stream must be readable.", nameof(package));

        options ??= new GamePackageImportOptions();

        if (options.MaxPackageBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "The maximum package size must be positive.");

        var packagePath = Path.Combine(Path.GetTempPath(), $"lancommander-import-{Guid.NewGuid():N}.lcx");
        long bytesTransferred = 0;

        try
        {
            await using (var output = new FileStream(
                packagePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[BufferSize];

                while (true)
                {
                    var read = await package.ReadAsync(buffer, cancellationToken);

                    if (read == 0)
                        break;

                    bytesTransferred += read;

                    if (bytesTransferred > options.MaxPackageBytes)
                        throw new InvalidDataException(
                            $"The package exceeds the maximum size of {options.MaxPackageBytes} bytes.");

                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    progress?.Report(new GamePackageImportProgress(
                        GamePackageImportStage.Copying,
                        bytesTransferred));
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new GamePackageImportProgress(
                GamePackageImportStage.Reading,
                bytesTransferred));

            progress?.Report(new GamePackageImportProgress(
                GamePackageImportStage.Importing,
                bytesTransferred));

            var result = await importRunner.RunFileAsync(
                packagePath,
                options.StorageLocationId,
                ManifestType.Game,
                cancellationToken);

            progress?.Report(new GamePackageImportProgress(
                GamePackageImportStage.Complete,
                bytesTransferred,
                result.ImportedCount));

            return new ImportResponse
            {
                RecordId = result.RecordId,
                ManifestType = result.ManifestType,
                ImportedCount = result.ImportedCount,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to import a plugin-provided game package");
            throw;
        }
        finally
        {
            try
            {
                if (File.Exists(packagePath))
                    File.Delete(packagePath);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not delete temporary game package {PackagePath}", packagePath);
            }
        }
    }
}
