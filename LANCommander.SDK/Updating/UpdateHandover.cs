#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LANCommander.SDK.Updating;

/// <summary>
/// Hands a running application over to its updated copy. The previous process applies the update,
/// starts the updated executable and stays alive until it reports a healthy start. If it never does,
/// the previous process (whose code is still in memory) stops it, restores the previous files and
/// starts itself again.
/// </summary>
public static class UpdateHandover
{
    /// <summary>Command line switch that applies the update in the given journal directory, then exits. Used to elevate.</summary>
    public const string ApplyArgument = "--ApplyUpdate";

    /// <summary>Command line switch that rolls back the update in the given journal directory, then exits. Used to elevate.</summary>
    public const string RollbackArgument = "--RollbackUpdate";

    /// <summary>
    /// Handles <see cref="ApplyArgument"/> and <see cref="RollbackArgument"/>. Returns the process exit
    /// code when one of them is present, or null when the process should start normally.
    /// </summary>
    public static int? RunFileOperation(string[] arguments, ILogger? logger = null)
    {
        var applyDirectory = GetArgumentValue(arguments, ApplyArgument);
        var rollbackDirectory = GetArgumentValue(arguments, RollbackArgument);

        if (applyDirectory == null && rollbackDirectory == null)
            return null;

        try
        {
            var journal = UpdateJournal.Load(applyDirectory ?? rollbackDirectory!);

            if (journal == null)
                return 2;

            if (applyDirectory != null)
            {
                SelfUpdater.Apply(journal, logger);
                return 0;
            }

            return SelfUpdater.Rollback(journal, logger) ? 0 : 1;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Could not run the update file operation");
            return 1;
        }
    }

    /// <summary>
    /// Applies the staged update, in this process when it can write to the install directory, or on
    /// Windows in an elevated copy of this executable when it can't.
    /// </summary>
    /// <param name="progress">Called with the files swapped so far and the total; not called when elevating.</param>
    public static async Task ApplyAsync(UpdateJournal journal, ILogger? logger = null, Action<int, int>? progress = null)
    {
        journal.PreviousProcessId = Environment.ProcessId;
        journal.Save();

        if (!RequiresElevation(journal))
        {
            SelfUpdater.Apply(journal, logger, progress);
            return;
        }

        await RunElevatedAsync(ApplyArgument, journal, logger);

        var result = UpdateJournal.Load(journal.Directory);

        if (result?.State != UpdateState.Applied)
            throw new InvalidOperationException("The update could not be applied with administrator rights");

        journal.State = result.State;
    }

    /// <summary>Rolls the update back, elevating on Windows when the install directory needs it.</summary>
    public static async Task<bool> RollbackAsync(UpdateJournal journal, ILogger? logger = null)
    {
        if (!RequiresElevation(journal))
            return SelfUpdater.Rollback(journal, logger);

        try
        {
            await RunElevatedAsync(RollbackArgument, journal, logger);

            return UpdateJournal.Load(journal.Directory)?.State == UpdateState.RolledBack;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Could not roll back update {Version} with administrator rights", journal.Version);
            return false;
        }
    }

    /// <summary>Starts the updated executable with <paramref name="arguments"/>.</summary>
    public static Process Start(UpdateJournal journal, IEnumerable<string> arguments, string workingDirectory) =>
        StartExecutable(journal.Executable, arguments, workingDirectory);

    /// <summary>Starts <paramref name="executable"/> as an ordinary, independent process.</summary>
    public static Process StartExecutable(string executable, IEnumerable<string> arguments, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            WorkingDirectory = workingDirectory,
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        return Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start {executable}");
    }

    /// <summary>
    /// Waits for the updated process to confirm a healthy start. Returns false, after stopping it, when
    /// it exits first or doesn't confirm within <paramref name="timeout"/>.
    /// </summary>
    public static async Task<bool> SuperviseAsync(UpdateJournal journal, Process process, TimeSpan timeout, ILogger? logger = null, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            if (IsConfirmed(journal))
            {
                logger?.LogInformation("Update {Version} confirmed by process {ProcessId}", journal.Version, process.Id);
                return true;
            }

            if (process.HasExited)
            {
                // It may have confirmed just before exiting
                if (IsConfirmed(journal))
                    return true;

                logger?.LogError("The updated process exited with code {ExitCode} before starting", process.ExitCode);
                return false;
            }

            if (stopwatch.Elapsed > timeout)
            {
                logger?.LogError("The updated process did not start within {Timeout}", timeout);

                Stop(process, logger);

                return false;
            }

            await Task.Delay(250, cancellationToken);
        }
    }

    private static bool IsConfirmed(UpdateJournal journal)
    {
        try
        {
            var current = UpdateJournal.Load(journal.Directory);

            // Gone means the updated process already finished the update
            return current == null || current.State is UpdateState.Confirmed or UpdateState.Completed;
        }
        catch
        {
            return false;
        }
    }

    private static void Stop(Process process, ILogger? logger)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);

            process.WaitForExit(10_000);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Could not stop the updated process");
        }
    }

    /// <summary>Whether changing the install directory needs administrator rights, which only Windows can ask for.</summary>
    public static bool RequiresElevation(UpdateJournal journal) =>
        OperatingSystem.IsWindows() && !Helpers.DirectoryHelper.IsDirectoryWritable(journal.InstallDirectory);

    private static async Task RunElevatedAsync(string argument, UpdateJournal journal, ILogger? logger)
    {
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("The running executable can't be found");

        logger?.LogInformation("Asking for administrator rights to update {InstallDirectory}", journal.InstallDirectory);

        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = Environment.CurrentDirectory,
            Arguments = $"{argument} \"{journal.Directory}\"",
        };

        Process? process;

        try
        {
            process = Process.Start(startInfo);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            throw new OperationCanceledException("Administrator rights were declined", ex);
        }

        if (process == null)
            throw new InvalidOperationException("Could not start the elevated update process");

        using (process)
            await process.WaitForExitAsync();
    }

    private static string? GetArgumentValue(string[] arguments, string name)
    {
        var index = Array.FindIndex(arguments, a => a.Equals(name, StringComparison.OrdinalIgnoreCase));

        return index < 0 || index + 1 >= arguments.Length ? null : arguments[index + 1];
    }
}
