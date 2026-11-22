#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LANCommander.SDK.Updating;

/// <summary>What a process must do about an update it finds while starting.</summary>
public enum UpdateRecoveryAction
{
    /// <summary>Nothing; start normally.</summary>
    None,

    /// <summary>An update was cut short and rolled back. The running files may be mixed, so restart.</summary>
    RestartRequired,
}

/// <summary>
/// Replaces an application's files with a staged update while the application is running, then
/// finishes or undoes the replacement. No separate updater executable is involved: a running program
/// can't overwrite its own files, but every desktop OS lets it rename them, so each installed file is
/// renamed aside and its replacement moved into its place. The renamed files are deleted once the
/// updated application has started and the previous process has exited.
/// </summary>
public static class SelfUpdater
{
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    private static readonly StringComparison PathComparison =
        OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    /// <summary>
    /// Starts a journal for a new update in <paramref name="journalDirectory"/>, first finishing or
    /// clearing an earlier update's. The journal is saved once its package is staged.
    /// </summary>
    public static UpdateJournal Begin(string journalDirectory, string version, UpdateInstallation installation, ILogger? logger = null)
    {
        var previous = UpdateJournal.Load(journalDirectory);

        switch (previous?.State)
        {
            case UpdateState.Applying or UpdateState.Applied:
                throw new InvalidOperationException($"Update {previous.Version} is still being applied");

            case UpdateState.Confirmed or UpdateState.Completed:
                Complete(previous, logger);
                break;

            case UpdateState.RolledBack:
                DeleteLeftovers(previous, logger, UpdateJournal.DiscardSuffix);
                break;
        }

        previous?.Delete();

        return UpdateJournal.Create(journalDirectory, version, installation);
    }

    /// <summary>
    /// Swaps the staged files into the install directory. On failure the swap is rolled back and the
    /// exception rethrown, leaving the installation as it was.
    /// </summary>
    /// <param name="progress">Called with the number of files swapped so far and the total.</param>
    public static void Apply(UpdateJournal journal, ILogger? logger = null, Action<int, int>? progress = null)
    {
        if (journal.State != UpdateState.Staged)
            throw new InvalidOperationException($"An update in the {journal.State} state can't be applied");

        var staged = EnumerateRelativeFiles(journal.StagingDirectory).ToList();

        if (staged.Count == 0)
            throw new InvalidOperationException("No files are staged for the update");

        var stagedSet = new HashSet<string>(staged, PathComparer);

        var unlisted = journal.RemoveUnlistedFiles
            ? EnumerateRelativeFiles(journal.InstallDirectory).Where(f => !stagedSet.Contains(f) && !IsUpdateLeftover(f)).ToList()
            : [];

        logger?.LogInformation("Applying update {Version} to {InstallDirectory}: {Count} files", journal.Version, journal.InstallDirectory, staged.Count);

        // Nothing has moved for a staged update, so any operations on record belong to an earlier one
        if (File.Exists(journal.OperationsPath))
            File.Delete(journal.OperationsPath);

        journal.State = UpdateState.Applying;
        journal.Save();

        var total = staged.Count + unlisted.Count;
        var done = 0;

        progress?.Invoke(0, total);

        try
        {
            using (var log = UpdateOperationLog.Open(journal.OperationsPath))
            {
                foreach (var relativePath in staged)
                {
                    var target = journal.GetInstalledPath(relativePath);

                    if (File.Exists(target) || IsSymbolicLink(target))
                    {
                        PrepareBackupPath(target + UpdateJournal.BackupSuffix);

                        log.Append(UpdateOperationKind.Backup, relativePath);
                        File.Move(target, target + UpdateJournal.BackupSuffix);
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);

                    log.Append(UpdateOperationKind.Install, relativePath);
                    File.Move(journal.GetStagedPath(relativePath), target);

                    progress?.Invoke(++done, total);
                }

                foreach (var relativePath in unlisted)
                {
                    var target = journal.GetInstalledPath(relativePath);

                    PrepareBackupPath(target + UpdateJournal.BackupSuffix);

                    log.Append(UpdateOperationKind.Backup, relativePath);
                    File.Move(target, target + UpdateJournal.BackupSuffix);

                    progress?.Invoke(++done, total);
                }
            }

            EnsureExecutable(journal.Executable);

            journal.State = UpdateState.Applied;
            journal.Save();

            logger?.LogInformation("Update {Version} applied", journal.Version);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Could not apply update {Version}, rolling back", journal.Version);

            Rollback(journal, logger);

            throw;
        }
    }

    /// <summary>
    /// Undoes a swap, in reverse order: updated files are moved aside and the previous files moved back.
    /// Safe to run on a swap that stopped part way. Returns whether every file was restored.
    /// </summary>
    public static bool Rollback(UpdateJournal journal, ILogger? logger = null)
    {
        var operations = UpdateOperationLog.Read(journal.OperationsPath);
        var restored = true;

        logger?.LogWarning("Rolling back update {Version}", journal.Version);

        foreach (var operation in operations.Reverse())
        {
            try
            {
                var target = journal.GetInstalledPath(operation.RelativePath);

                switch (operation.Kind)
                {
                    // The update's file, or a partial copy of it. Renamed rather than deleted, since it
                    // may be the running executable.
                    case UpdateOperationKind.Install when File.Exists(target) || IsSymbolicLink(target):
                        MoveAside(target, UpdateJournal.DiscardSuffix);
                        break;

                    case UpdateOperationKind.Backup when File.Exists(target + UpdateJournal.BackupSuffix) || IsSymbolicLink(target + UpdateJournal.BackupSuffix):
                        File.Move(target + UpdateJournal.BackupSuffix, target);
                        break;
                }
            }
            catch (Exception ex)
            {
                restored = false;
                logger?.LogError(ex, "Could not roll back {Path}", operation.RelativePath);
            }
        }

        DeleteLeftovers(journal, logger, UpdateJournal.DiscardSuffix);

        journal.State = UpdateState.RolledBack;
        journal.Save();

        return restored;
    }

    /// <summary>
    /// Deletes the files the update replaced and the journal. Files still in use stay behind, marked
    /// in the journal as <see cref="UpdateState.Completed"/>, and are deleted on a later start.
    /// Returns whether everything was deleted.
    /// </summary>
    public static bool Complete(UpdateJournal journal, ILogger? logger = null)
    {
        var remaining = DeleteLeftovers(journal, logger);

        if (remaining > 0)
        {
            logger?.LogInformation("{Count} replaced files are still in use and will be deleted later", remaining);

            journal.State = UpdateState.Completed;
            journal.Save();

            return false;
        }

        journal.Delete();

        logger?.LogInformation("Update {Version} complete", journal.Version);

        return true;
    }

    /// <summary>
    /// Run as early as possible while starting. Rolls back an update that was cut short in the middle of
    /// its swap and clears out one that was rolled back.
    /// </summary>
    public static UpdateRecoveryAction Recover(string journalDirectory, ILogger? logger = null)
    {
        UpdateJournal? journal;

        try
        {
            journal = UpdateJournal.Load(journalDirectory);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Could not read the update journal in {Directory}", journalDirectory);
            return UpdateRecoveryAction.None;
        }

        switch (journal?.State)
        {
            case UpdateState.Applying:
                logger?.LogWarning("Update {Version} was interrupted while applying", journal.Version);

                Rollback(journal, logger);

                return UpdateRecoveryAction.RestartRequired;

            case UpdateState.RolledBack:
                DeleteLeftovers(journal, logger, UpdateJournal.DiscardSuffix);
                journal.Delete();
                break;
        }

        return UpdateRecoveryAction.None;
    }

    /// <summary>
    /// Run once the application has started far enough to be considered healthy. Tells a waiting
    /// previous process that the update worked, waits for it to exit, then deletes the replaced files.
    /// </summary>
    public static async Task ConfirmStartupAsync(string journalDirectory, ILogger? logger = null, CancellationToken cancellationToken = default)
    {
        ConfirmStartup(journalDirectory, logger);

        await FinishAsync(journalDirectory, logger, cancellationToken);
    }

    /// <summary>
    /// Tells a waiting previous process that the update worked. Returns the update's journal when this
    /// call is the one that confirmed it, so the caller can tell the user what changed.
    /// </summary>
    public static UpdateJournal? ConfirmStartup(string journalDirectory, ILogger? logger = null)
    {
        var journal = UpdateJournal.Load(journalDirectory);

        if (journal?.State != UpdateState.Applied)
            return null;

        journal.State = UpdateState.Confirmed;
        journal.Save();

        logger?.LogInformation("Update {Version} started successfully", journal.Version);

        return journal;
    }

    /// <summary>
    /// Finishes a confirmed update: waits for the previous process to exit, then deletes the files the
    /// update replaced.
    /// </summary>
    public static async Task FinishAsync(string journalDirectory, ILogger? logger = null, CancellationToken cancellationToken = default)
    {
        var journal = UpdateJournal.Load(journalDirectory);

        if (journal?.State is not (UpdateState.Confirmed or UpdateState.Completed))
            return;

        // The previous process's files can't be deleted on Windows while it's still running
        if (journal.PreviousProcessId is { } previousProcessId && previousProcessId != Environment.ProcessId)
            await WaitForExitAsync(previousProcessId, TimeSpan.FromMinutes(2), cancellationToken);

        Complete(journal, logger);
    }

    /// <summary>Waits for a process to exit, giving up after <paramref name="timeout"/>.</summary>
    public static async Task<bool> WaitForExitAsync(int processId, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        Process process;

        try
        {
            process = Process.GetProcessById(processId);
        }
        catch (ArgumentException)
        {
            return true;
        }

        using (process)
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            timeoutSource.CancelAfter(timeout);

            try
            {
                await process.WaitForExitAsync(timeoutSource.Token);
                return true;
            }
            catch (OperationCanceledException)
            {
                return process.HasExited;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }
    }

    /// <summary>
    /// Deletes the files the journal's operations left beside the installed ones, with
    /// <paramref name="suffixes"/> (by default both kinds). Returns how many couldn't be deleted.
    /// </summary>
    private static int DeleteLeftovers(UpdateJournal journal, ILogger? logger, params string[] suffixes)
    {
        if (suffixes.Length == 0)
            suffixes = [UpdateJournal.BackupSuffix, UpdateJournal.DiscardSuffix];

        var remaining = 0;

        foreach (var relativePath in UpdateOperationLog.Read(journal.OperationsPath).Select(o => o.RelativePath).Distinct(PathComparer))
        {
            string target;

            try
            {
                target = journal.GetInstalledPath(relativePath);
            }
            catch (InvalidOperationException)
            {
                continue;
            }

            var directory = Path.GetDirectoryName(target)!;

            if (!Directory.Exists(directory))
                continue;

            // Includes the numbered names MoveAside falls back to
            foreach (var suffix in suffixes)
            foreach (var leftover in Directory.EnumerateFiles(directory, Path.GetFileName(target) + "*" + suffix))
            {
                if (!IsLeftoverOf(target, leftover, suffix))
                    continue;

                try
                {
                    File.Delete(leftover);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    remaining++;
                    logger?.LogDebug(ex, "Could not delete {Path} yet", leftover);
                }
            }
        }

        return remaining;
    }

    /// <summary>Whether <paramref name="candidate"/> is <paramref name="target"/> plus the suffix, or plus a number and the suffix.</summary>
    private static bool IsLeftoverOf(string target, string candidate, string suffix)
    {
        if (!candidate.EndsWith(suffix, StringComparison.Ordinal) || !candidate.StartsWith(target, PathComparison))
            return false;

        var middle = candidate[target.Length..^suffix.Length];

        return middle.Length == 0 || (middle[0] == '.' && middle.Length > 1 && middle[1..].All(Char.IsDigit));
    }

    /// <summary>Clears the way for a backup, deleting one an earlier, unfinished update left.</summary>
    private static void PrepareBackupPath(string backup)
    {
        if (File.Exists(backup) || IsSymbolicLink(backup))
            File.Delete(backup);
    }

    private static void MoveAside(string path, string suffix)
    {
        var aside = path + suffix;

        for (var attempt = 1; File.Exists(aside) || IsSymbolicLink(aside); attempt++)
        {
            try
            {
                File.Delete(aside);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Still in use from an earlier attempt
                aside = $"{path}.{attempt}{suffix}";
            }
        }

        File.Move(path, aside);
    }

    private static IEnumerable<string> EnumerateRelativeFiles(string directory)
    {
        if (!Directory.Exists(directory))
            return [];

        return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(directory, f));
    }

    private static bool IsUpdateLeftover(string path) =>
        path.EndsWith(UpdateJournal.BackupSuffix, StringComparison.Ordinal)
        || path.EndsWith(UpdateJournal.DiscardSuffix, StringComparison.Ordinal);

    private static bool IsSymbolicLink(string path)
    {
        try
        {
            return new FileInfo(path).LinkTarget != null;
        }
        catch
        {
            return false;
        }
    }

    private static void EnsureExecutable(string executable)
    {
        if (OperatingSystem.IsWindows() || !File.Exists(executable))
            return;

        File.SetUnixFileMode(executable, File.GetUnixFileMode(executable) | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
    }
}
