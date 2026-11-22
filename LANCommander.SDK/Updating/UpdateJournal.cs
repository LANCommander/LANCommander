#nullable enable
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace LANCommander.SDK.Updating;

/// <summary>
/// The record of one self-update, kept in its own directory so any process taking part in the update
/// (the running application, an elevated copy of it, or the updated application) can pick it up:
/// <code>
/// Updates/
///   Journal.json     this file: what is being updated, and how far it got
///   Operations.log   every file move, written before the move happens
///   Staging/         the extracted package
/// </code>
/// </summary>
public sealed class UpdateJournal
{
    public const string JournalFileName = "Journal.json";
    public const string OperationsFileName = "Operations.log";
    public const string StagingDirectoryName = "Staging";

    /// <summary>Suffix of an installed file that was moved aside to make room for its replacement.</summary>
    public const string BackupSuffix = ".UpdateBackup";

    /// <summary>Suffix of an updated file that was moved aside while rolling an update back.</summary>
    public const string DiscardSuffix = ".UpdateDiscard";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>The version being installed.</summary>
    public string Version { get; set; } = String.Empty;

    /// <summary>The version being replaced.</summary>
    public string? PreviousVersion { get; set; }

    /// <summary>The directory whose files are replaced. For a macOS app, the .app bundle.</summary>
    public string InstallDirectory { get; set; } = String.Empty;

    /// <summary>The executable to start once the files are swapped.</summary>
    public string Executable { get; set; } = String.Empty;

    /// <summary>
    /// Whether installed files the package doesn't contain are moved aside too. Only safe where the
    /// application owns the whole install directory, as it does a macOS app bundle.
    /// </summary>
    public bool RemoveUnlistedFiles { get; set; }

    public UpdateState State { get; set; }

    /// <summary>The process that applied the update. The updated process waits for it to exit before deleting the replaced files.</summary>
    public int? PreviousProcessId { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    [JsonIgnore]
    public string Directory { get; private set; } = String.Empty;

    [JsonIgnore]
    public string FilePath => Path.Combine(Directory, JournalFileName);

    [JsonIgnore]
    public string OperationsPath => Path.Combine(Directory, OperationsFileName);

    [JsonIgnore]
    public string StagingDirectory => Path.Combine(Directory, StagingDirectoryName);

    public static UpdateJournal Create(string directory, string version, UpdateInstallation installation) => new()
    {
        Directory = NormalizeDirectory(directory),
        Version = version,
        InstallDirectory = installation.InstallDirectory,
        Executable = installation.Executable,
        RemoveUnlistedFiles = installation.IsBundle,
        State = UpdateState.Staged,
        CreatedOn = DateTimeOffset.UtcNow,
    };

    public static bool Exists(string directory) => File.Exists(Path.Combine(directory, JournalFileName));

    /// <summary>Reads the journal in <paramref name="directory"/>, or returns null when there is none.</summary>
    public static UpdateJournal? Load(string directory)
    {
        var path = Path.Combine(directory, JournalFileName);

        return Retry(() =>
        {
            if (!File.Exists(path))
                return null;

            // Share delete so a writer in another process can replace the file while this reads it
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            var journal = JsonSerializer.Deserialize<UpdateJournal>(stream, SerializerOptions);

            if (journal != null)
                journal.Directory = NormalizeDirectory(directory);

            return journal;
        });
    }

    /// <summary>Writes the journal to a temporary file and moves it into place, so a reader never sees half of it.</summary>
    public void Save()
    {
        System.IO.Directory.CreateDirectory(Directory);

        var temporaryPath = FilePath + ".tmp";

        Retry<object?>(() =>
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(this, SerializerOptions));
            File.Move(temporaryPath, FilePath, overwrite: true);

            return null;
        });
    }

    /// <summary>Deletes the journal, its operations and the staging directory.</summary>
    public void Delete()
    {
        TryDeleteDirectory(StagingDirectory);
        TryDeleteFile(OperationsPath);
        TryDeleteFile(FilePath);
    }

    public string GetInstalledPath(string relativePath)
    {
        var root = Path.GetFullPath(InstallDirectory);
        var path = Path.GetFullPath(Path.Combine(root, relativePath));

        if (!path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException($"The update path {relativePath} is outside of the install directory");

        return path;
    }

    public string GetStagedPath(string relativePath) => Path.Combine(StagingDirectory, relativePath);

    private static string NormalizeDirectory(string directory) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));

    private static T Retry<T>(Func<T> action)
    {
        // Another process taking part in the update may be reading or replacing the file at the same moment
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return action();
            }
            catch (IOException) when (attempt < 20)
            {
                Thread.Sleep(50);
            }
            catch (UnauthorizedAccessException) when (attempt < 20)
            {
                Thread.Sleep(50);
            }
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Left for the next update to clear
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (System.IO.Directory.Exists(path))
                System.IO.Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Left for the next update to clear
        }
    }
}
