namespace LANCommander.Server.Plugins;

/// <summary>High-level stages reported while importing a game package.</summary>
public enum GamePackageImportStage
{
    Copying,
    Reading,
    Importing,
    Complete,
}

/// <summary>Progress reported by <see cref="IGamePackageImporter"/>.</summary>
public sealed record GamePackageImportProgress(
    GamePackageImportStage Stage,
    long BytesTransferred = 0,
    int ImportedCount = 0);
