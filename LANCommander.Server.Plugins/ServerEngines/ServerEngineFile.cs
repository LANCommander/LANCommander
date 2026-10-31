namespace LANCommander.Server.Plugins;

/// <summary>
/// A file or folder among a server's files, for <see cref="ServerEngineFeatures.Files"/>; see
/// <see cref="IServerEngineExtension.ListFilesAsync"/>.
/// </summary>
/// <param name="Name">The entry's name within the folder listed, without any path.</param>
/// <param name="IsDirectory">Whether it is a folder.</param>
/// <param name="Size">A file's length in bytes, where the engine knows it.</param>
/// <param name="LastModified">When it was last written, in UTC, where the engine knows.</param>
public sealed record ServerEngineFile(string Name, bool IsDirectory, long? Size = null, DateTime? LastModified = null);
