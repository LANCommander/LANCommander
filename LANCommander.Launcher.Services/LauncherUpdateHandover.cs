using LANCommander.SDK.Updating;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services
{
    public enum LauncherUpdateStage
    {
        Downloading,
        WaitingForAdministrator,
        Installing,
        Starting,
        RollingBack,
    }

    /// <param name="Percent">0 to 100, or null while there's nothing to measure.</param>
    public sealed record LauncherUpdateProgress(LauncherUpdateStage Stage, string Status, double? Percent);

    /// <summary>
    /// Swaps in a staged update once the launcher's window has closed and its single-instance lock is
    /// released, then starts the updated launcher and waits until it reports a healthy start. If it
    /// doesn't, the update is rolled back, the reason saved for the restarted launcher to show, and this
    /// version started again.
    /// </summary>
    public static class LauncherUpdateHandover
    {
        // Covers the updated launcher's database migrations, which run before it confirms
        private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(3);

        private static bool _restartRequested;

        /// <summary>Whether the launcher should start again once its window closes.</summary>
        public static bool IsRestartRequested => _restartRequested;

        /// <summary>Whether this process has handed over to another launcher and should exit.</summary>
        public static bool HasHandedOver { get; private set; }

        /// <summary>Starts the launcher again once the window closes.</summary>
        public static void RequestRestart() => _restartRequested = true;

        /// <summary>Starts the launcher again, from its own executable.</summary>
        public static void Restart(string[] arguments)
        {
            UpdateHandover.StartExecutable(Environment.ProcessPath!, arguments, Environment.CurrentDirectory);

            HasHandedOver = true;
        }

        /// <summary>
        /// Installs the update staged in <paramref name="journalDirectory"/> and starts it. Whatever
        /// happens, a launcher is running afterwards: the updated one, or this version again. Returns
        /// whether the update installed.
        /// </summary>
        /// <param name="arguments">The arguments to start the launcher with.</param>
        public static async Task<bool> InstallAsync(string journalDirectory, string[] arguments, IProgress<LauncherUpdateProgress> progress, ILogger? logger)
        {
            HasHandedOver = true;

            var executable = Environment.ProcessPath!;
            var journal = UpdateJournal.Load(journalDirectory);

            if (journal?.State != UpdateState.Staged)
            {
                logger?.LogWarning("No staged update was found in {Directory}", journalDirectory);

                Fail(journalDirectory, journal?.Version ?? String.Empty, journal?.PreviousVersion, "The downloaded update could not be found.");
                UpdateHandover.StartExecutable(executable, arguments, Environment.CurrentDirectory);

                return false;
            }

            try
            {
                if (UpdateHandover.RequiresElevation(journal))
                    progress.Report(new(LauncherUpdateStage.WaitingForAdministrator, "Waiting for administrator approval...", null));
                else
                    progress.Report(new(LauncherUpdateStage.Installing, "Installing update...", 0));

                await UpdateHandover.ApplyAsync(journal, logger, (done, total) =>
                    progress.Report(new(LauncherUpdateStage.Installing, $"Installing update ({done} of {total} files)...", total == 0 ? null : 100d * done / total)));
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Could not install launcher update {Version}", journal.Version);

                Fail(journalDirectory, journal.Version, journal.PreviousVersion, ex is OperationCanceledException
                    ? "Administrator approval was declined, so the update could not be installed. It will be installed the next time you update."
                    : $"The update's files could not be installed: {ex.Message}");

                UpdateHandover.StartExecutable(executable, arguments, Environment.CurrentDirectory);

                return false;
            }

            progress.Report(new(LauncherUpdateStage.Starting, $"Starting LANCommander {journal.Version}...", null));

            string? error;

            try
            {
                var process = UpdateHandover.Start(journal, arguments, Environment.CurrentDirectory);

                if (await UpdateHandover.SuperviseAsync(journal, process, StartupTimeout, logger))
                    return true;

                error = process.HasExited
                    ? $"LANCommander {journal.Version} closed while starting (exit code {process.ExitCode})."
                    : $"LANCommander {journal.Version} did not finish starting within {StartupTimeout.TotalMinutes:0} minutes.";
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Could not start launcher {Version}", journal.Version);

                error = $"LANCommander {journal.Version} could not be started: {ex.Message}";
            }

            progress.Report(new(LauncherUpdateStage.RollingBack, "Restoring the previous version...", null));

            var restored = await UpdateHandover.RollbackAsync(journal, logger);

            logger?.LogWarning("Launcher update {Version} was rolled back", journal.Version);

            Fail(journalDirectory, journal.Version, journal.PreviousVersion, restored
                ? error
                : $"{error} Some of the previous version's files could not be restored; reinstall the launcher if it misbehaves.");

            UpdateHandover.StartExecutable(executable, arguments, Environment.CurrentDirectory);

            return false;
        }

        private static void Fail(string journalDirectory, string version, string? previousVersion, string error)
        {
            try
            {
                new UpdateResult { Version = version, PreviousVersion = previousVersion, Error = error }.Save(journalDirectory);
            }
            catch
            {
                // The restarted launcher just won't explain what happened
            }
        }
    }
}
