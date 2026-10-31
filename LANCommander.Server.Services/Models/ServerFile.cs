namespace LANCommander.Server.Services.Models;

/// <summary>
/// A file or folder among a game server's files, as listed by <see cref="ServerManager.ListFilesAsync"/>.
/// </summary>
/// <param name="Name">The entry's name within the folder listed.</param>
/// <param name="Path">Its path relative to the server's files, with "/" between folders.</param>
/// <param name="IsDirectory">Whether it is a folder.</param>
/// <param name="Size">A file's length in bytes, where known.</param>
/// <param name="LastModified">When it was last written, in UTC, where known.</param>
public sealed record ServerFile(string Name, string Path, bool IsDirectory, long? Size, DateTime? LastModified);
