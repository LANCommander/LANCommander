using LANCommander.SDK.Models;

namespace LANCommander.Server.Plugins;

/// <summary>Imports an LCX game package through the server's canonical import pipeline.</summary>
public interface IGamePackageImporter
{
    /// <summary>
    /// Imports a package stream and returns the created or updated game. Cancellation is
    /// cooperative; the record currently being written is completed before cancellation is
    /// observed, and completed records are not rolled back.
    /// </summary>
    Task<ImportResponse> ImportAsync(
        Stream package,
        GamePackageImportOptions? options = null,
        IProgress<GamePackageImportProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
