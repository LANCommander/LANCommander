namespace LANCommander.Server.Plugins;

/// <summary>Controls how an LCX game package is imported.</summary>
public sealed class GamePackageImportOptions
{
    /// <summary>Archive storage location to use, or null for the server default.</summary>
    public Guid? StorageLocationId { get; init; }

    /// <summary>Maximum accepted package size in bytes.</summary>
    public long MaxPackageBytes { get; init; } = 100L * 1024 * 1024 * 1024;
}
