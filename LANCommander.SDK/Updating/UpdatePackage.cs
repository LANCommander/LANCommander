#nullable enable
using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace LANCommander.SDK.Updating;

/// <summary>Checks a downloaded update and puts its files in an update's staging directory.</summary>
public static class UpdatePackage
{
    // Unix file type bits of a zip entry's external attributes (st_mode in the high 16 bits)
    private const int UnixFileTypeMask = 0xF000;
    private const int UnixSymbolicLink = 0xA000;

    /// <summary>Returns the lowercase hex SHA-256 of a file.</summary>
    public static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);

        var hash = await SHA256.HashDataAsync(stream, cancellationToken);

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Whether a file matches the expected SHA-256. An empty expectation matches anything.</summary>
    public static async Task<bool> VerifyAsync(string path, string? expectedSha256, CancellationToken cancellationToken = default)
    {
        if (String.IsNullOrWhiteSpace(expectedSha256))
            return true;

        var actual = await ComputeSha256Async(path, cancellationToken);

        return actual.Equals(expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Stages a downloaded package for <paramref name="journal"/>'s installation: an archive is extracted,
    /// an AppImage is copied in under the name of the AppImage it replaces.
    /// </summary>
    public static void Stage(UpdateJournal journal, string packagePath, UpdatePackageKind kind)
    {
        ResetDirectory(journal.StagingDirectory);

        if (kind == UpdatePackageKind.AppImage)
        {
            var staged = journal.GetStagedPath(Path.GetFileName(journal.Executable));

            File.Copy(packagePath, staged, overwrite: true);

            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(staged, File.GetUnixFileMode(staged) | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
        }
        else
        {
            ExtractArchive(packagePath, journal.StagingDirectory, journal.RemoveUnlistedFiles);
        }

        if (Directory.GetFileSystemEntries(journal.StagingDirectory).Length == 0)
            throw new InvalidDataException("The update package contains no files");
    }

    /// <summary>
    /// Extracts a zip into <paramref name="stagingDirectory"/>. With <paramref name="bundle"/>, the archive
    /// holds a macOS app bundle (Name.app/...) and only that bundle's contents are extracted.
    /// </summary>
    public static void ExtractArchive(string archivePath, string stagingDirectory, bool bundle)
    {
        var root = Path.GetFullPath(stagingDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        Directory.CreateDirectory(root);

        using var archive = ZipFile.OpenRead(archivePath);

        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName.Replace('\\', '/');

            if (bundle)
            {
                var separator = name.IndexOf('/');

                // Anything beside the bundle isn't part of the app
                if (separator < 0 || !name[..separator].EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                    continue;

                name = name[(separator + 1)..];
            }

            if (name.Length == 0)
                continue;

            var destination = Path.GetFullPath(Path.Combine(root, name));

            if (!destination.StartsWith(root, StringComparison.Ordinal))
                throw new InvalidDataException($"The update package entry {entry.FullName} points outside of its directory");

            if (name.EndsWith('/'))
            {
                Directory.CreateDirectory(destination);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

            if (!OperatingSystem.IsWindows() && ((entry.ExternalAttributes >> 16) & UnixFileTypeMask) == UnixSymbolicLink)
            {
                using var reader = new StreamReader(entry.Open());

                File.CreateSymbolicLink(destination, reader.ReadToEnd());
                continue;
            }

            // Restores the entry's Unix permissions, including the executable bit
            entry.ExtractToFile(destination, overwrite: true);
        }
    }

    private static void ResetDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);

        Directory.CreateDirectory(path);
    }
}
